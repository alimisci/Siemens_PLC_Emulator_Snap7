using System;
using System.Collections.Generic;
using System.Linq;

namespace S7Emulator
{
    /// <summary>
    /// Supported Siemens S7 data types.
    /// </summary>
    public enum S7DataType
    {
        Bool,
        Byte,
        Word,
        Int,
        DWord,
        DInt,
        Real,
        String
    }

    public static class S7DataTypeInfo
    {
        /// <summary>
        /// Returns the fixed byte size for the given type (String is excluded).
        /// Bool and String are handled separately.
        /// </summary>
        public static int FixedByteSize(S7DataType type)
        {
            switch (type)
            {
                case S7DataType.Bool: return 0; // bit-based, handled separately
                case S7DataType.Byte: return 1;
                case S7DataType.Word: return 2;
                case S7DataType.Int: return 2;
                case S7DataType.DWord: return 4;
                case S7DataType.DInt: return 4;
                case S7DataType.Real: return 4;
                default: throw new InvalidOperationException("String size must be computed from StringLength.");
            }
        }

        /// <summary>
        /// On S7 controllers, WORD/INT/DWORD/DINT/REAL and STRING fields conventionally
        /// start at an even (word-aligned) byte offset.
        /// </summary>
        public static bool RequiresWordAlignment(S7DataType type)
        {
            return type != S7DataType.Bool && type != S7DataType.Byte;
        }
    }

    /// <summary>
    /// Definition of a single field (tag) inside a DB.
    /// </summary>
    public class DbFieldDefinition
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public S7DataType DataType { get; set; } = S7DataType.Bool;

        /// <summary>Byte offset inside the buffer (calculated automatically).</summary>
        public int Offset { get; set; }

        /// <summary>Bool only: bit number 0-7 (calculated automatically).</summary>
        public int BitOffset { get; set; }

        /// <summary>String only: maximum character count.</summary>
        public int StringLength { get; set; } = 20;

        /// <summary>Total byte size of this field (used for offset calculations).</summary>
        public int ByteSize
        {
            get
            {
                if (DataType == S7DataType.Bool) return 0;
                if (DataType == S7DataType.String) return StringLength + 2; // S7 STRING header: maxlen + actuallen
                return S7DataTypeInfo.FixedByteSize(DataType);
            }
        }

        public string Address
        {
            get
            {
                if (DataType == S7DataType.Bool)
                    return $"DBX{Offset}.{BitOffset}";
                string prefix = DataType switch
                {
                    S7DataType.Byte => "DBB",
                    S7DataType.Word => "DBW",
                    S7DataType.Int => "DBW",
                    S7DataType.DWord => "DBD",
                    S7DataType.DInt => "DBD",
                    S7DataType.Real => "DBD",
                    S7DataType.String => "DBB",
                    _ => "DB"
                };
                return $"{prefix}{Offset}";
            }
        }
    }

    /// <summary>
    /// Definition of a single Data Block (DB): number, fields and the backing byte buffer.
    /// </summary>
    public class DbDefinition
    {
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public List<DbFieldDefinition> Fields { get; } = new List<DbFieldDefinition>();

        /// <summary>The actual buffer registered with the Snap7 S7Server. Updated after RecalculateLayout.</summary>
        public byte[] Buffer { get; private set; } = new byte[1];

        /// <summary>
        /// Recalculates offsets in field-list order and rebuilds the buffer to the new size
        /// (attempts to preserve existing values).
        /// </summary>
        public void RecalculateLayout()
        {
            int currentByte = 0;
            int currentBit = 0;

            foreach (var field in Fields)
            {
                if (field.DataType == S7DataType.Bool)
                {
                    field.Offset = currentByte;
                    field.BitOffset = currentBit;
                    currentBit++;
                    if (currentBit == 8)
                    {
                        currentBit = 0;
                        currentByte++;
                    }
                }
                else
                {
                    // Close out a partially-filled byte left over from bit fields
                    if (currentBit != 0)
                    {
                        currentBit = 0;
                        currentByte++;
                    }

                    if (S7DataTypeInfo.RequiresWordAlignment(field.DataType) && currentByte % 2 != 0)
                        currentByte++;

                    field.Offset = currentByte;
                    field.BitOffset = 0;
                    currentByte += field.ByteSize;
                }
            }

            int totalSize = currentByte + (currentBit != 0 ? 1 : 0);
            if (totalSize < 1) totalSize = 1;

            var newBuffer = new byte[totalSize];

            // Try to preserve existing values by copying the old buffer into the new one.
            // This is a simple best-effort copy; if field order/types change, values may shift.
            if (Buffer != null)
            {
                int copyLen = Math.Min(Buffer.Length, newBuffer.Length);
                Array.Copy(Buffer, newBuffer, copyLen);
            }

            Buffer = newBuffer;
        }

        public DbFieldDefinition? FindField(string name)
        {
            return Fields.FirstOrDefault(f => f.Name == name);
        }
    }
}
