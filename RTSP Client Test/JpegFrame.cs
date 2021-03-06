﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Media.Rtp
{
	/// <summary>
	/// Implements RFC2435
	/// </summary>
	public class JpegFrame : RtpFrame
	{

		#region Statics

		static byte[] CreateStartOfInformation()
		{
			return new byte[] { 0xff, 0xd8 };
		}

		static byte[] CreateJFIFHeader(uint type, uint width, uint height, ArraySegment<byte> tables, uint dri)
		{
			List<byte> result = new List<byte>();
			result.AddRange(CreateStartOfInformation());

			result.Add(0xff);
			result.Add(0xe0);//AppFirst
			result.Add(0x00);
			result.Add(0x10);//length
			result.Add((byte)'J');
			result.Add((byte)'F');
			result.Add((byte)'I');
			result.Add((byte)'F');
			result.Add(0x00);

			result.Add(0x01);//Version Major
			result.Add(0x01);//Version Minor

			result.Add(0x00);//Units

			result.Add(0x00);//Horizontal
			result.Add(0x01);

			result.Add(0x00);//Vertical
			result.Add(0x01);

			result.Add(0x00);//No thumb
			result.Add(0x00);

			if (dri > 0)
			{
				result.AddRange(CreateDataRestartIntervalMarker(dri));
			}

			result.AddRange(CreateQuantizationTablesMarker(tables));

			result.Add(0xff);
			result.Add(0xc0);//SOF
			result.Add(0x00);
			result.Add(0x11);
			result.Add(0x08);
			result.Add((byte)(height >> 8));
			result.Add((byte)height);
			result.Add((byte)(width >> 8));
			result.Add((byte)width);

			result.Add(0x03);
			result.Add(0x01);
			result.Add((byte)(type > 0 ? 0x22 : 0x21));
			result.Add(0x00);

			result.Add(0x02);
			result.Add(0x11);
			result.Add(0x01);

			result.Add(0x03);
			result.Add(0x11);
			result.Add(0x01);

			//Huffman Tables
			result.AddRange(CreateHuffmanTableMarker(lum_dc_codelens, lum_dc_symbols, 0, 0));
			result.AddRange(CreateHuffmanTableMarker(lum_ac_codelens, lum_ac_symbols, 0, 1));
			result.AddRange(CreateHuffmanTableMarker(chm_dc_codelens, chm_dc_symbols, 1, 0));
			result.AddRange(CreateHuffmanTableMarker(chm_ac_codelens, chm_ac_symbols, 1, 1));

			result.Add(0xff);
			result.Add(0xda);//Marker SOS
			result.Add(0x00);
			result.Add(0x0c);
			result.Add(0x03);
			result.Add(0x01);
			result.Add(0x00);
			result.Add(0x02);
			result.Add(0x11);
			result.Add(0x03);
			result.Add(0x11);
			result.Add(0x00);
			result.Add(0x3f);
			result.Add(0x00);

			return result.ToArray();
		}

		// The default 'luma' and 'chroma' quantizer tables, in zigzag order:
		static byte[] defaultQuantizers = new byte[]
		{
           // luma table:
           16, 11, 12, 14, 12, 10, 16, 14,
		   13, 14, 18, 17, 16, 19, 24, 40,
		   26, 24, 22, 22, 24, 49, 35, 37,
		   29, 40, 58, 51, 61, 60, 57, 51,
		   56, 55, 64, 72, 92, 78, 64, 68,
		   87, 69, 55, 56, 80, 109, 81, 87,
		   95, 98, 103, 104, 103, 62, 77, 113,
		   121, 112, 100, 120, 92, 101, 103, 99,
           // chroma table:
           17, 18, 18, 24, 21, 24, 47, 26,
		   26, 47, 99, 66, 56, 66, 99, 99,
		   99, 99, 99, 99, 99, 99, 99, 99,
		   99, 99, 99, 99, 99, 99, 99, 99,
		   99, 99, 99, 99, 99, 99, 99, 99,
		   99, 99, 99, 99, 99, 99, 99, 99,
		   99, 99, 99, 99, 99, 99, 99, 99,
		   99, 99, 99, 99, 99, 99, 99, 99
		};

		/// <summary>
		/// Creates a Luma and Chroma Table using the default quantizer
		/// </summary>
		/// <param name="Q">The quality</param>
		/// <returns>64 luma bytes and 64 chroma</returns>
		static byte[] CreateQuantizationTables(uint Q)
		{
			int factor = (int)Q;
			int q;

			if (Q < 1) factor = 1;
			else if (Q > 99) factor = 99;

			if (Q < 50)
			{
				q = 5000 / factor;
			}
			else
			{
				q = 200 - factor * 2;
			}
			byte[] resultTables = new byte[128];
			for (int i = 0; i < 128; ++i)
			{
				int newVal = (defaultQuantizers[i] * q + 50) / 100;
				if (newVal < 1) newVal = 1;
				else if (newVal > 255) newVal = 255;
				resultTables[i] = (byte)newVal;
			}
			return resultTables;
		}

		static byte[] CreateQuantizationTablesMarker(ArraySegment<byte> tables)
		{
			List<byte> result = new List<byte>();

			int tableSize = tables.Count / 2;

			//Luma

			result.Add(0xff);
			result.Add(0xdb);

			result.Add(0x00);
			result.Add((byte)(tableSize + 3));
			result.Add(0x00);

			for (int i = 0, e = tableSize; i < e; ++i)
			{
				result.Add(tables.Array[tables.Offset + i]);
			}

			//Chroma

			result.Add(0xff);
			result.Add(0xdb);

			result.Add(0x00);
			result.Add((byte)(tableSize + 3));
			result.Add(0x01);

			for (int i = tableSize, e = tables.Count; i < e; ++i)
			{
				result.Add(tables.Array[tables.Offset + i]);
			}

			return result.ToArray();

		}

		static byte[] lum_dc_codelens = {
		0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0,
		};

		static byte[] lum_dc_symbols = {
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
		};

		static byte[] lum_ac_codelens = {
		0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d,
		};

		static byte[] lum_ac_symbols = {
		0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
		0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
		0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
		0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0,
		0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16,
		0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
		0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
		0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
		0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
		0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
		0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
		0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
		0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
		0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
		0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
		0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5,
		0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4,
		0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
		0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
		0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
		0xf9, 0xfa,
		};

		static byte[] chm_dc_codelens = {
		0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0,
		};

		static byte[] chm_dc_symbols = {
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
		};

		static byte[] chm_ac_codelens = {
		0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77,
		};

		static byte[] chm_ac_symbols = {
		0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21,
		0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
		0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
		0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0,
		0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34,
		0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
		0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38,
		0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
		0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
		0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
		0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
		0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
		0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96,
		0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5,
		0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
		0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
		0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2,
		0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
		0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9,
		0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
		0xf9, 0xfa,
		};

		static byte[] CreateHuffmanTableMarker(byte[] codeLens, byte[] symbols, int tableNo, int tableClass)
		{
			List<byte> result = new List<byte>();
			result.Add(0xff);
			result.Add(0xc4);
			result.Add(0x00);
			result.Add((byte)(3 + codeLens.Length + symbols.Length));
			result.Add((byte)((tableClass << 4) | tableNo));
			result.AddRange(codeLens);
			result.AddRange(symbols);
			return result.ToArray();
		}

		static byte[] CreateDataRestartIntervalMarker(uint dri)
		{
			return new byte[] { 0xff, 0xdd, 0x00, 0x04, (byte)(dri >> 8), (byte)(dri) };
		}

		#endregion

		internal System.IO.MemoryStream Buffer = new System.IO.MemoryStream();

		internal System.Drawing.Image Image;

		public JpegFrame() : base(26) { }

		public JpegFrame(RtpFrame f) : base(f) { if (PayloadType != 26) throw new ArgumentException("Expected the payload type 26, Found type: " + f.PayloadType); }

		/// <summary>
		/// //Writes the packets to a memory stream and created the default header and quantization tables if necessary.
		/// </summary>
		internal void ProcessPackets(RtpPacket packet)
		{
			ArraySegment<byte> tables = default(ArraySegment<byte>);
			uint FragmentOffset, TypeSpecific, Type, Quality, Width, Height, DataRestartInterval = 0;
			//for (int i = 0, e = this.Packets.Count, last = e - 1; i < e; ++i)
			{
				int offset = 0;

				//RtpPacket packet = this.Packets[i];
				byte[] payloadBytes = packet.PayloadData.ToArray();

				TypeSpecific = (uint)(payloadBytes[offset++]);

				//FragmentOffset = (uint)(payloadBytes[offset++] | payloadBytes[offset++] << 8 | payloadBytes[offset++] << 16);
				FragmentOffset = (uint)(payloadBytes[offset++] << 16 | payloadBytes[offset++] << 8 | payloadBytes[offset++]);

				//Ensure start packet
				//if (i == 0 && FragmentOffset != 0) continue;

				Type = (uint)(payloadBytes[offset++]);
				Quality = (uint)payloadBytes[offset++];
				Width = (uint)(payloadBytes[offset++] * 8);
				Height = (uint)(payloadBytes[offset++] * 8);

				//Only occur in the first packet
				if (FragmentOffset == 0)
				{
					//If There is already a StartOfInformation present just copy
					if (payloadBytes[offset] == 0xff && payloadBytes[offset + 1] == 0xd8)
					{
						goto WritePacket;
					}

					//Get the restart interval is present
					if (Type > 63)
					{
						DataRestartInterval = (uint)(payloadBytes[offset++] << 8 | payloadBytes[offset++]);
					}

					//Get the Q Tables if present
					if (Quality > 127)
					{
						uint MBZ = (uint)(payloadBytes[offset++]);//Should only proceed if MBZ > 0?
						uint Precision = (uint)(payloadBytes[offset++]);//Wasted read
						uint Length = (uint)(payloadBytes[offset++] << 8 | payloadBytes[offset++]);
						if (Length > 0)
						{
							tables = new ArraySegment<byte>(payloadBytes, offset, (int)Length);
							offset += (int)Length;
						}
						else
						{
							tables = new ArraySegment<byte>(CreateQuantizationTables(Quality));
						}
					}

					//Write the header to the buffer
					byte[] header = CreateJFIFHeader(Type, Width, Height, tables, DataRestartInterval);
					Buffer.Write(header, 0, header.Length);

				}

				WritePacket:    //Copy rest of data to buffer
				Buffer.Write(payloadBytes, offset, payloadBytes.Length - offset);

				//Check to see if EOI is present or needs to be added on the last packet
				if (/*i == last && */payloadBytes[payloadBytes.Length - 2] != 0xff && payloadBytes[payloadBytes.Length - 1] != 0xd9)
				{
					Buffer.WriteByte(0xff);
					Buffer.WriteByte(0xd9);
				}

			}

			//Go back to the beginning
			Buffer.Seek(0, System.IO.SeekOrigin.Begin);
		}

		/// <summary>
		/// Creates a image from the processed packets in the memory stream
		/// </summary>
		/// <returns>The image created from the packets</returns>
		internal System.Drawing.Image ToImage(RtpPacket packet)
		{
			try
			{
				if (Image != null) return Image;
				ProcessPackets(packet);
				return Image = System.Drawing.Image.FromStream(Buffer, false, true);
			}
			catch { throw; }
			finally
			{
				Buffer.Dispose();
				Buffer = null;
			}

		}
	}
}
#if original
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Media.Rtp;

namespace RTSP_Client_Test
{
    /// <summary>
    /// Implements RFC2435
    /// </summary>
    public class JpegFrame : RtpFrame
    {

#region Statics   static byte[] StartOfInformation = new byte[] { 0xff, 0xd8 };   static byte[] EndOfInformation = new byte[] { 0xff, 0xd9 };
           static byte[] CreateJFIFHeader(uint type, uint width, uint height, ArraySegment<byte> tables, uint dri)
        {
            List<byte> result = new List<byte>();
            result.AddRange(StartOfInformation);       result.Add(0xff);
            result.Add(0xe0);//AppFirst
            result.Add(0x00);
            result.Add(0x10);//length
            result.Add((byte)'J');
            result.Add((byte)'F');
            result.Add((byte)'I');
            result.Add((byte)'F');
            result.Add(0x00);       result.Add(0x01);//Version Major
            result.Add(0x01);//Version Minor       result.Add(0x00);//Units       result.Add(0x00);//Horizontal
            result.Add(0x01);       result.Add(0x00);//Vertical
            result.Add(0x01);       result.Add(0x00);//No thumb
            result.Add(0x00);       if (dri > 0)
            {
                result.AddRange(CreateDataRestartIntervalMarker(dri));
            }       result.AddRange(CreateQuantizationTablesMarker(tables));       result.Add(0xff);
            result.Add(0xc0);//SOF
            result.Add(0x00);
            result.Add(0x11);
            result.Add(0x08);
            result.Add((byte)(height >> 8));
            result.Add((byte)height);
            result.Add((byte)(width >> 8));
            result.Add((byte)width);
            
            result.Add(0x03);
            result.Add(0x01);
            result.Add((byte)(type > 0 ? 0x22 : 0x21));
            result.Add(0x00);       result.Add(0x02);
            result.Add(0x11);
            result.Add(0x01);       result.Add(0x03);
            result.Add(0x11);
            result.Add(0x01);       //Huffman Tables
            result.AddRange(CreateHuffmanTableMarker(lum_dc_codelens, lum_dc_symbols, 0, 0));
            result.AddRange(CreateHuffmanTableMarker(lum_ac_codelens, lum_ac_symbols, 0, 1));
            result.AddRange(CreateHuffmanTableMarker(chm_dc_codelens, chm_dc_symbols, 1, 0));
            result.AddRange(CreateHuffmanTableMarker(chm_ac_codelens, chm_ac_symbols, 1, 1));
            
            result.Add(0xff);
            result.Add(0xda);//Marker SOS
            result.Add(0x00);
            result.Add(0x0c);
            result.Add(0x03);
            result.Add(0x01);
            result.Add(0x00);
            result.Add(0x02);
            result.Add(0x11);
            result.Add(0x03);
            result.Add(0x11);
            result.Add(0x00);
            result.Add(0x3f);
            result.Add(0x00);       return result.ToArray();
        }   // The default 'luma' and 'chroma' quantizer tables, in zigzag order:
        static byte[] defaultQuantizers = new byte[]
        {
           // luma table:
           16, 11, 12, 14, 12, 10, 16, 14,
           13, 14, 18, 17, 16, 19, 24, 40,
           26, 24, 22, 22, 24, 49, 35, 37,
           29, 40, 58, 51, 61, 60, 57, 51,
           56, 55, 64, 72, 92, 78, 64, 68,
           87, 69, 55, 56, 80, 109, 81, 87,
           95, 98, 103, 104, 103, 62, 77, 113,
           121, 112, 100, 120, 92, 101, 103, 99,
           // chroma table:
           17, 18, 18, 24, 21, 24, 47, 26,
           26, 47, 99, 66, 56, 66, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99,
           99, 99, 99, 99, 99, 99, 99, 99
        };        /// <summary>
        /// Creates a Luma and Chroma Table using the default quantizer
        /// </summary>
        /// <param name="Q">The quality</param>
        /// <returns>64 luma bytes and 64 chroma</returns>
        static byte[] CreateQuantizationTables(uint Q)
        {
            int factor = (int)Q;
            int q;       if (Q < 1) factor = 1;
            else if (Q > 99) factor = 99;       if (Q < 50)
            {
                q = 5000 / factor;
            }
            else
            {
                q = 200 - factor * 2;
            }
            byte[] resultTables = new byte[128];
            for (int i = 0; i < 128; ++i)
            {
                int newVal = (defaultQuantizers[i] * q + 50) / 100;
                if (newVal < 1) newVal = 1;
                else if (newVal > 255) newVal = 255;
                resultTables[i] = (byte)newVal;
            }
            return resultTables;
        }   static byte[] CreateQuantizationTablesMarker(ArraySegment<byte> tables)
        {
            List<byte> result = new List<byte>();       int tableSize = tables.Count / 2;       //Luma       result.Add(0xff);
            result.Add(0xdb);       result.Add(0x00);
            result.Add((byte)(tableSize + 3));
            result.Add(0x00);       for (int i = 0, e = tableSize; i < e; ++i)
            {
                result.Add(tables.Array[tables.Offset + i]);
            }       //Chroma       result.Add(0xff);
            result.Add(0xdb);       result.Add(0x00);
            result.Add((byte)(tableSize + 3));
            result.Add(0x01);       for (int i = tableSize, e = tables.Count; i < e; ++i)
            {
                result.Add(tables.Array[tables.Offset + i]);
            }       return result.ToArray();   }   static byte[] lum_dc_codelens = {
        0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0,
        };   static byte[] lum_dc_symbols = {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };   static byte[] lum_ac_codelens = {
        0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d,
        };   static byte[] lum_ac_symbols = {
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12,
        0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
        0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0,
        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16,
        0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
        0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39,
        0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
        0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
        0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
        0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79,
        0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
        0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98,
        0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
        0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
        0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5,
        0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4,
        0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
        0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea,
        0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
        0xf9, 0xfa,
        };   static byte[] chm_dc_codelens = {
        0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0,
        };   static byte[] chm_dc_symbols = {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 
        };   static byte[] chm_ac_codelens = {
        0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77,
        };   static byte[] chm_ac_symbols = {
        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21,
        0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
        0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0,
        0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34,
        0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
        0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38,
        0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
        0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
        0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
        0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78,
        0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
        0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96,
        0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5,
        0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
        0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3,
        0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2,
        0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
        0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9,
        0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8,
        0xf9, 0xfa,
        };   static byte[] CreateHuffmanTableMarker(byte[] codeLens, byte[] symbols, int tableNo, int tableClass)
        {
            List<byte> result = new List<byte>();
            result.Add(0xff);
            result.Add(0xc4);
            result.Add(0x00);
            result.Add((byte)(3 + codeLens.Length + symbols.Length));
            result.Add((byte)((tableClass << 4) | tableNo));
            result.AddRange(codeLens);
            result.AddRange(symbols);
            return result.ToArray();
        }   static byte[] CreateDataRestartIntervalMarker(uint dri)
        {
            return new byte[] { 0xff, 0xdd, 0x00, 0x04, (byte)(dri >> 8), (byte)(dri) };
        }

#endregion   internal System.IO.MemoryStream Buffer = new System.IO.MemoryStream();   internal System.Drawing.Image Image;   public JpegFrame() : base(26) { }   public JpegFrame(RtpFrame f) : base(f)
		{
			//if (PayloadType != 26) throw new ArgumentException("Expected the payload type 26, Found type: " + f.PayloadType);
		}
		
        /// <summary>
        /// //Writes the packets to a memory stream and created the default header and quantization tables if necessary.
        /// </summary>
        internal void ProcessPackets(RtpPacket packet)
        {
            ArraySegment<byte> tables = default(ArraySegment<byte>);
            int offset = 0;       uint TypeSpecific, FragmentOffset, Type, Quality, Width, Height, DataRestartInterval = 0;           TypeSpecific = (uint)(packet.Payload.Array[0]);
                FragmentOffset = (uint)(packet.Payload.Array[1] << 16 | packet.Payload.Array[2] << 8 | packet.Payload.Array[3]);
                Type = (uint)(packet.Payload.Array[4]); //&1 for type
                Quality = (uint)packet.Payload.Array[5];
                Width = (uint)(packet.Payload.Array[6] * 8);
                Height = (uint)(packet.Payload.Array[7] * 8);
                offset = 8;           //Only occur in the first packet
                if (FragmentOffset == 0)
                {
                    //If There is already a StartOfInformation header present just copy it
                    if (packet.Payload.Array[offset] == StartOfInformation[0] && packet.Payload.Array[offset] == StartOfInformation[1])
                    {
                        goto WritePacket;
                    }               if (Type > 63)
                    {
                        DataRestartInterval = (uint)(packet.Payload.Array[offset++] << 8 | packet.Payload.Array[offset++]);
                    }               if (Quality > 127)
                    {
                        uint MBZ = (uint)(packet.Payload.Array[offset++]);
                        uint Precision = (uint)(packet.Payload.Array[offset++]);
                        uint Length = (uint)(packet.Payload.Array[offset++] << 8 | packet.Payload.Array[offset++]);
                        if(Length > 0)
                        {
                            tables = new ArraySegment<byte>(packet.Payload.Array, offset, (int)Length);
                            offset += (int)Length;
                        }
                        else
                        {
                            tables = new ArraySegment<byte>(CreateQuantizationTables(Quality));
                        }
                    }               //Write the header to the buffer
                    byte[] header = CreateJFIFHeader(Type, Width, Height, tables, DataRestartInterval);
                    Buffer.Write(header, 0, header.Length);           }
                
            WritePacket:    //Copy rest of data to buffer
                Buffer.Write(packet.Payload.Array, offset, packet.Payload.Array.Count - offset);              //Check for EOI Marker
            Buffer.Seek(Buffer.Length - 2, System.IO.SeekOrigin.Begin);       if(Buffer.ReadByte() != EndOfInformation[0] && Buffer.ReadByte() != EndOfInformation[1])
            {
                Buffer.Write(EndOfInformation, 0, EndOfInformation.Length);
            }       //Go back to the beginning
            Buffer.Seek(0, System.IO.SeekOrigin.Begin);            
        }   /// <summary>
        /// Creates a image from the processed packets in the memory stream
        /// </summary>
        /// <returns>The image created from the packets</returns>
        internal System.Drawing.Image ToImage(RtpPacket packet)
        {
            try
            {
                if (Image != null) return Image;
                ProcessPackets(packet);
				return Image = System.Drawing.Image.FromStream(Buffer);//, false, true);
            }
            catch
            {
                throw;
            }
        }
    }
}
#endif