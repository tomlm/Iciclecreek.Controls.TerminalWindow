using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using Avalonia;

namespace Iciclecreek.Avalonia.Terminal.Buffer
{
    internal class PixelBuffer
    {
        private readonly List<Pixel[]> _rows;

        public PixelBuffer(ushort width, ushort height)
        {
            Width = width;
            Height = height;
            _rows = new List<Pixel[]>(height);
            for (ushort y = 0; y < height; y++)
                _rows.Add(CreateBlankRow());
        }

        // Maximum visible dimensions
#pragma warning disable CA1051
        public readonly ushort Width;
        public readonly ushort Height;
#pragma warning restore CA1051

        [JsonIgnore]
        public int Length => Width * Height;

        [JsonIgnore]
        public PixelRect Size => new(0, 0, Width, Height);

        // Indexer by linear index
        [JsonIgnore]
        public Pixel this[int i]
        {
            get
            {
                (ushort x, ushort y) = ToXY(i);
                return this[x, y];
            }
            set
            {
                (ushort x, ushort y) = ToXY(i);
                this[x, y] = value;
            }
        }

        // Indexer (ushort)
        [JsonIgnore]
        public Pixel this[ushort x, ushort y]
        {
            get => _rows[y][x];
            set => _rows[y][x] = value;
        }

        // Indexer (int)
        [JsonIgnore]
        public Pixel this[int x, int y]
        {
            get => _rows[y][x];
            set => _rows[y][x] = value;
        }

        // Indexer by point
        [JsonIgnore]
        public Pixel this[PixelPoint point]
        {
            get => _rows[point.Y][point.X];
            set => _rows[point.Y][point.X] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (ushort x, ushort y) ToXY(int i)
        {
            return ((ushort)(i % Width), (ushort)(i / Width));
        }

        private Pixel[] CreateBlankRow()
        {
            var row = new Pixel[Width];
            for (int x = 0; x < Width; x++)
                row[x] = Pixel.Space;
            return row;
        }

        /// <summary>
        /// Scrolls the buffer up one line: removes the first row and appends a blank row at the bottom.
        /// </summary>
        public void ScrollUp()
        {
            // Remove first (top) row
            if (_rows.Count > 0)
                _rows.RemoveAt(0);
            // Append new blank row at bottom
            _rows.Add(CreateBlankRow());
        }

        /// <summary>
        /// Clears a row (fills with space pixels).
        /// </summary>
        public void ClearRow(int y)
        {
            var row = _rows[y];
            for (int x = 0; x < Width; x++)
                row[x] = Pixel.Space;
        }

        /// <summary>
        /// Returns a copy of the row (for inspection). Modifications to returned array are not applied.
        /// </summary>
        public Pixel[] GetRowSnapshot(int y)
        {
            var src = _rows[y];
            var copy = new Pixel[Width];
            Array.Copy(src, copy, Width);
            return copy;
        }

        /// <summary>
        /// Appends a new logical line (provided pixels) applying scroll if necessary.
        /// The incoming array length must match Width.
        /// </summary>
        public void AppendLineScrolling(Pixel[] line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            if (line.Length != Width)
                throw new ArgumentException("Line width mismatch.", nameof(line));

            // Scroll and replace last row with provided line
            ScrollUp();
            _rows[Height - 1] = line;
        }

        public string PrintBuffer()
        {
            var sb = new StringBuilder();
            for (ushort y = 0; y < Height; y++)
            {
                var row = _rows[y];
                for (ushort x = 0; x < Width;)
                {
                    Pixel pixel = row[x];
                    if (pixel.Width > 0)
                    {
                        sb.Append(pixel.Symbol.GetText());
                        x += pixel.Width;
                    }
                    else
                    {
                        x++;
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}