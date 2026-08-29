using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MapDescShow;

public sealed record MapPreview(string Path, int Width, int Height, int CellBytes, long TrailingBytes,
    Bitmap Bitmap, int WalkableCount, int MoveBlockedCastAllowedCount,
    int MoveAndCastBlockedCount, int ClosedDoorCount);

public static class MapFileReader
{
    public const int HeaderSize = 52;
    // 客户端目录中同时存在经典 12 字节、扩展 14 字节，以及每格保留到 36 字节的格式。
    // 三种格式的阻挡判断字段均位于开头相同位置；36 字节格式的后续字节不参与通行预览。
    private static readonly int[] SupportedCellSizes = [12, 14, 36];

    public static MapPreview ReadWalkability(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.SequentialScan);
        using var reader = new BinaryReader(stream);

        if (stream.Length < HeaderSize)
            throw new InvalidDataException("文件小于 52 字节，不是支持的传奇 MAP。\n" + path);

        int width = reader.ReadUInt16();
        int height = reader.ReadUInt16();
        if (width <= 0 || height <= 0 || width > 20000 || height > 20000)
            throw new InvalidDataException($"地图尺寸异常：{width} × {height}");

        long cellCount = (long)width * height;
        long payloadSize = stream.Length - HeaderSize;
        int cellSize = SupportedCellSizes.FirstOrDefault(size => payloadSize == cellCount * size);
        // 一些旧地图缩小过头部宽高，但文件尾仍保留旧的完整单元数据。
        // 它们的数据区仍严格由 12 字节单元组成，有效区域以头部宽高为准。
        if (cellSize == 0 && payloadSize >= cellCount * 12 && payloadSize % 12 == 0)
            cellSize = 12;
        if (cellSize == 0)
        {
            string detected = payloadSize % cellCount == 0
                ? $"检测到每格 {payloadSize / cellCount} 字节"
                : $"数据区 {payloadSize:N0} 字节不能被 {cellCount:N0} 个格子整除";
            throw new InvalidDataException($"暂不支持的 MAP 单元格式：{detected}。当前支持每格 12、14 或 36 字节。\n{path}");
        }
        long trailingBytes = payloadSize - cellCount * cellSize;

        stream.Position = HeaderSize;
        var pixels = new byte[checked(width * height * 4)];
        var column = new byte[checked(height * cellSize)];
        int walkableCount = 0;
        int moveBlockedCastAllowedCount = 0;
        int moveAndCastBlockedCount = 0;
        int closedDoorCount = 0;

        // 原版客户端按 X 列存储：offset = 52 + (x * height + y) * 12。
        for (int x = 0; x < width; x++)
        {
            stream.ReadExactly(column);
            for (int y = 0; y < height; y++)
            {
                int cell = y * cellSize;
                ushort background = (ushort)(column[cell] | column[cell + 1] << 8);
                ushort foreground = (ushort)(column[cell + 4] | column[cell + 5] << 8);
                byte doorIndex = column[cell + 6];
                byte doorOffset = column[cell + 7];

                bool moveBlockedCastAllowed = (background & 0x8000) != 0;
                bool moveAndCastBlocked = (foreground & 0x8000) != 0;
                bool closedDoor = (doorIndex & 0x80) != 0 && (doorOffset & 0x80) == 0;
                bool walkable = !moveBlockedCastAllowed && !moveAndCastBlocked;
                if (closedDoor)
                    walkable = false;

                // 两种 $8000 标志都禁止移动，因此预览统一画黑色；仅统计时区分施法能力。
                if (moveAndCastBlocked)
                    moveAndCastBlockedCount++;
                else if (moveBlockedCastAllowed)
                    moveBlockedCastAllowedCount++;
                if (closedDoor) closedDoorCount++;

                int pixel = (y * width + x) * 4;
                if (walkable)
                {
                    pixels[pixel + 1] = 190; // G
                    pixels[pixel + 3] = 255; // A
                    walkableCount++;
                }
                else
                {
                    pixels[pixel + 3] = 255;
                }
            }
        }

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            if (data.Stride == width * 4)
            {
                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            else
            {
                for (int y = 0; y < height; y++)
                    Marshal.Copy(pixels, y * width * 4, data.Scan0 + y * data.Stride, width * 4);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return new MapPreview(path, width, height, cellSize, trailingBytes, bitmap, walkableCount,
            moveBlockedCastAllowedCount, moveAndCastBlockedCount, closedDoorCount);
    }
}
