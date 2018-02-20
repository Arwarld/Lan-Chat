using System;
using System.Collections.Generic;
using System.Text;

namespace Lan_Chat
{
    class Datagram
    {
        List<byte> data;
        Encoding enc = new UTF8Encoding(true, true);
        public Datagram(byte id)
        {
            data = new List<byte>();
            data.Add(id);
        }
        public Datagram(byte[] data)
        {
            this.data = new List<byte>(data);
        }
        public void AppendInteger(int value)
        {
            data.AddRange(BitConverter.GetBytes(value));
        }
        public void AppendLong(long value)
        {
            data.AddRange(BitConverter.GetBytes(value));
        }
        public void AppendString(string value)
        {
            AppendInteger(enc.GetByteCount(value));
            data.AddRange(enc.GetBytes(value));
        }

        public void AppendBytes(byte[] value)
        {
            data.AddRange(value);
        }

        public byte[] Data
        {
            get
            {
                return data.ToArray();
            }
        }
        public int ReadInt(ref int pos)
        {
            int value = BitConverter.ToInt32(data.ToArray(), pos);
            pos += 4;
            return value;
        }
        public long Readlong(ref int pos)
        {
            long value = BitConverter.ToInt64(data.ToArray(), pos);
            pos += 8;
            return value;
        }
        public byte[] ReadBytes(ref int pos, int bytes)
        {
            byte[] value = new byte[bytes];

            Array.Copy(data.ToArray(), pos, value, 0, bytes);

            return value;
        }


        public byte ID
        {
            get { return data[0]; }
            set { data[0] = value; }
        }

        public string ReadString(ref int pos)
        {
            int length = ReadInt(ref pos);
            string value = enc.GetString(data.ToArray(), pos, length);
            pos += length;
            return value;
        }
    }
}
