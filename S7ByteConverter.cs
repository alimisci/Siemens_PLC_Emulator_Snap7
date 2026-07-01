using System;
using System.Text;

namespace S7Emulator
{
    /// <summary>
    /// The Siemens S7 protocol uses big-endian byte order, while .NET's BitConverter
    /// assumes little-endian. So we implement the conversions manually with explicit
    /// control over byte order.
    /// </summary>
    public static class S7ByteConverter
    {
        public static bool GetBool(byte[] buf, int byteOffset, int bitOffset)
        {
            if (byteOffset >= buf.Length) return false;
            return (buf[byteOffset] & (1 << bitOffset)) != 0;
        }

        public static void SetBool(byte[] buf, int byteOffset, int bitOffset, bool value)
        {
            if (byteOffset >= buf.Length) return;
            if (value)
                buf[byteOffset] |= (byte)(1 << bitOffset);
            else
                buf[byteOffset] &= (byte)~(1 << bitOffset);
        }

        public static byte GetByte(byte[] buf, int offset) => offset < buf.Length ? buf[offset] : (byte)0;

        public static void SetByte(byte[] buf, int offset, byte value)
        {
            if (offset < buf.Length) buf[offset] = value;
        }

        public static ushort GetWord(byte[] buf, int offset)
        {
            if (offset + 1 >= buf.Length) return 0;
            return (ushort)((buf[offset] << 8) | buf[offset + 1]);
        }

        public static void SetWord(byte[] buf, int offset, ushort value)
        {
            if (offset + 1 >= buf.Length) return;
            buf[offset] = (byte)(value >> 8);
            buf[offset + 1] = (byte)(value & 0xFF);
        }

        public static short GetInt(byte[] buf, int offset) => (short)GetWord(buf, offset);

        public static void SetInt(byte[] buf, int offset, short value) => SetWord(buf, offset, (ushort)value);

        public static uint GetDWord(byte[] buf, int offset)
        {
            if (offset + 3 >= buf.Length) return 0;
            return ((uint)buf[offset] << 24) | ((uint)buf[offset + 1] << 16) |
                   ((uint)buf[offset + 2] << 8) | buf[offset + 3];
        }

        public static void SetDWord(byte[] buf, int offset, uint value)
        {
            if (offset + 3 >= buf.Length) return;
            buf[offset] = (byte)(value >> 24);
            buf[offset + 1] = (byte)(value >> 16);
            buf[offset + 2] = (byte)(value >> 8);
            buf[offset + 3] = (byte)(value & 0xFF);
        }

        public static int GetDInt(byte[] buf, int offset) => (int)GetDWord(buf, offset);

        public static void SetDInt(byte[] buf, int offset, int value) => SetDWord(buf, offset, (uint)value);

        public static float GetReal(byte[] buf, int offset)
        {
            uint bits = GetDWord(buf, offset);
            byte[] tmp = BitConverter.GetBytes(bits);
            // BitConverter.GetBytes(uint) returns bytes in the host's endianness; convert
            // to the host's expected order before turning it into a float.
            if (BitConverter.IsLittleEndian) Array.Reverse(tmp);
            return BitConverter.ToSingle(tmp, 0);
        }

        public static void SetReal(byte[] buf, int offset, float value)
        {
            byte[] tmp = BitConverter.GetBytes(value); // host endianness
            if (BitConverter.IsLittleEndian) Array.Reverse(tmp);
            uint bits = BitConverter.ToUInt32(tmp, 0);
            SetDWord(buf, offset, bits);
        }

        /// <summary>
        /// S7 STRING layout: [0]=MaxLen, [1]=ActualLen, [2..]=ASCII characters.
        /// </summary>
        public static string GetString(byte[] buf, int offset, int maxLength)
        {
            if (offset + 1 >= buf.Length) return "";
            int actualLen = buf[offset + 1];
            actualLen = Math.Min(actualLen, maxLength);
            int available = buf.Length - (offset + 2);
            actualLen = Math.Min(actualLen, Math.Max(available, 0));
            if (actualLen <= 0) return "";
            return Encoding.ASCII.GetString(buf, offset + 2, actualLen);
        }

        public static void SetString(byte[] buf, int offset, int maxLength, string value)
        {
            if (offset + 1 >= buf.Length) return;
            if (value == null) value = "";
            if (value.Length > maxLength) value = value.Substring(0, maxLength);

            buf[offset] = (byte)maxLength;
            buf[offset + 1] = (byte)value.Length;

            byte[] chars = Encoding.ASCII.GetBytes(value);
            int writeLen = Math.Min(chars.Length, buf.Length - (offset + 2));
            if (writeLen > 0)
                Array.Copy(chars, 0, buf, offset + 2, writeLen);
        }

        /// <summary>
        /// Reads a field's value from the buffer according to its type and returns it as a string (for the UI).
        /// </summary>
        public static string ReadFieldAsString(byte[] buf, DbFieldDefinition field)
        {
            switch (field.DataType)
            {
                case S7DataType.Bool: return GetBool(buf, field.Offset, field.BitOffset) ? "1" : "0";
                case S7DataType.Byte: return GetByte(buf, field.Offset).ToString();
                case S7DataType.Word: return GetWord(buf, field.Offset).ToString();
                case S7DataType.Int: return GetInt(buf, field.Offset).ToString();
                case S7DataType.DWord: return GetDWord(buf, field.Offset).ToString();
                case S7DataType.DInt: return GetDInt(buf, field.Offset).ToString();
                case S7DataType.Real: return GetReal(buf, field.Offset).ToString(System.Globalization.CultureInfo.InvariantCulture);
                case S7DataType.String: return GetString(buf, field.Offset, field.StringLength);
                default: return "";
            }
        }

        /// <summary>
        /// Parses a string value coming from the UI and writes it into the buffer as the
        /// field's native type. Returns false (and leaves the buffer untouched) if parsing fails.
        /// </summary>
        public static bool TryWriteFieldFromString(byte[] buf, DbFieldDefinition field, string text)
        {
            try
            {
                switch (field.DataType)
                {
                    case S7DataType.Bool:
                        bool b = text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase);
                        SetBool(buf, field.Offset, field.BitOffset, b);
                        return true;
                    case S7DataType.Byte:
                        SetByte(buf, field.Offset, byte.Parse(text));
                        return true;
                    case S7DataType.Word:
                        SetWord(buf, field.Offset, ushort.Parse(text));
                        return true;
                    case S7DataType.Int:
                        SetInt(buf, field.Offset, short.Parse(text));
                        return true;
                    case S7DataType.DWord:
                        SetDWord(buf, field.Offset, uint.Parse(text));
                        return true;
                    case S7DataType.DInt:
                        SetDInt(buf, field.Offset, int.Parse(text));
                        return true;
                    case S7DataType.Real:
                        SetReal(buf, field.Offset, float.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
                        return true;
                    case S7DataType.String:
                        SetString(buf, field.Offset, field.StringLength, text);
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
