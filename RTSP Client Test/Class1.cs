using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RTSP_Client_Test
{
    class Class1
    {

		/// <summary>
		/// After a single RtpPacket is <see cref="Depacketize">depacketized</see> it will be placed into this list with the appropriate index.
		/// </summary>
		internal readonly SortedList<int, Media.Common.MemorySegment> Depacketized = new SortedList<int, Media.Common.MemorySegment>();


		public void ProcessPacket(Media.Rtp.RtpPacket packet, bool rfc2035Quality = false, bool useRfcQuantizer = false) //Should give tables here and should have option to check for EOI
		{

			if (Media.Common.IDisposedExtensions.IsNullOrDisposed(packet)) return;

			//Depacketize the single packet to Depacketized.

			/*
			 * 
			 * 3.1.  JPEG header
			   Each packet contains a special JPEG header which immediately follows
			   the RTP header.  The first 8 bytes of this header, called the "main
			   JPEG header", are as follows:

				0                   1                   2                   3
				0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
			   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			   | Type-specific |              Fragment Offset                  |
			   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			   |      Type     |       Q       |     Width     |     Height    |
			   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
			 * 
			 All fields in this header except for the Fragment Offset field MUST
			 remain the same in all packets that correspond to the same JPEG
			 frame.

			 A Restart Marker header and/or Quantization Table header may follow this header, depending on the values of the Type and Q fields.                 
			 */

			byte TypeSpecific, Type, Quality,
			//A byte which is bit mapped, each bit indicates 16 bit coeffecients for the table .
			PrecisionTable = 0;

			uint FragmentOffset, Width, Height;

			ushort RestartInterval = 0, RestartCount;

			//Payload starts at the offset of the first PayloadOctet within the Payload segment after the sourceList or extensions.
			int offset = packet.Payload.Offset + packet.HeaderOctets,
				padding = packet.PaddingOctets,
				count = (packet.Payload.Count - padding),
				end = packet.Length - padding;

			//ProfileHeaderInformation.MinimumProfileHeaderSize

			//Need 8 bytes.
			if (count < 8 || offset > packet.Payload.Count) return;

			//if (packet.Extension) throw new NotSupportedException("RFC2035 nor RFC2435 defines extensions.");

			Media.Common.MemorySegment tables;

			//We will depacketize something and may need to inspect the last bytes of the memory added.
			Media.Common.MemorySegment depacketized = null;

			//Decode RtpJpeg Header

			//Should verify values after first packet....

			TypeSpecific = (packet.Payload.Array[offset++]);

			FragmentOffset = Media.Common.Binary.ReadU24(packet.Payload.Array, ref offset, BitConverter.IsLittleEndian); //(uint)(packet.Payload.Array[offset++] << 16 | packet.Payload.Array[offset++] << 8 | packet.Payload.Array[offset++]);

			//Todo, should preserve order even when FragmentOffset and Sequence Number wraps.
			//May not provide the correct order when sequenceNumber approaches ushort.MaxValue...
			//int packetKey = GetPacketKey(packet.SequenceNumber);//Depacketized.Count > 0 ? Depacketized.Keys.Last() + 1 : 0; //packet.Timestamp - packet.SequenceNumber;

			int packetKey = packet.SequenceNumber;

			//add FragmentOffset to the PacketKey to ensure that rollover is hanlded correctly and that the data is placed at the appropriate place in te stream
			//Because of this, this implementation successfully can receive data sent from a sender in which the fragmentOffsets are in the reverse of the sending order.
			//I will make a test to prove this out shortly.
			//The idea is that the sender can send the last fragment in the first packet...
			//The result is that the packet data index is based on the fragmentOffset and the seqeuence number
			//Meaning that if it rolls over the sequence number still puts it in the right place with respect to where it needs to be.
			//Senders can send with fragmentOffset == 0 as the last packet with the marker if they so choose.
			//No OTHER SOFTWARE I KNOW OF PERSONALLY will not work like that.....
			//Even if they re-order packets correctly.
			//This is because the FragmentOffset should take precedence with respect to the SeqeuenceNumber
			//And furthernmore if it wraps again to 0 the reciever should be smart enough to detect this and still place it in the resultant buffer correctly.
			//packetKey &= (int)FragmentOffset;

			//Seq   | Frag     |key | index
			//65535 | 200      | 199| 2
			//0     | 300      | 300| 3
			//1     | 100      | 101| 1
			//2     | 0 Marker | 2  | 0         -> Depacketized.Count > 0, would not read QTables (needs state)

			//Seq   | Frag     |key      | index
			//65535 | 16777215 | 16777214| 3
			//0     | 0        | 0       | 0    -> Depacketized.Count > 0, would not read QTables (needs state)
			//1     | 100      | 101     | 1
			//2     | 300  M   | 302     | 2    


			//Already contained, todo verify. Also verify offsets of copy.
			if (Depacketized.ContainsKey(packetKey)) return;

			#region RFC2435 -  The Type Field

			/*
				 4.1.  The Type Field

The Type field defines the abbreviated table-specification and
additional JFIF-style parameters not defined by JPEG, since they are
not present in the body of the transmitted JPEG data.

Three ranges of the type field are currently defined. Types 0-63 are
reserved as fixed, well-known mappings to be defined by this document
and future revisions of this document. Types 64-127 are the same as
types 0-63, except that restart markers are present in the JPEG data
and a Restart Marker header appears immediately following the main
JPEG header. Types 128-255 are free to be dynamically defined by a
session setup protocol (which is beyond the scope of this document).

Of the first group of fixed mappings, types 0 and 1 are currently
defined, along with the corresponding types 64 and 65 that indicate
the presence of restart markers.  They correspond to an abbreviated
table-specification indicating the "Baseline DCT sequential" mode,
8-bit samples, square pixels, three components in the YUV color
space, standard Huffman tables as defined in [1, Annex K.3], and a
single interleaved scan with a scan component selector indicating
components 1, 2, and 3 in that order.  The Y, U, and V color planes
correspond to component numbers 1, 2, and 3, respectively.  Component
1 (i.e., the luminance plane) uses Huffman table number 0 and
quantization table number 0 (defined below) and components 2 and 3
(i.e., the chrominance planes) use Huffman table number 1 and
quantization table number 1 (defined below).

Type numbers 2-5 are reserved and SHOULD NOT be used.  Applications
based on previous versions of this document (RFC 2035) should be
updated to indicate the presence of restart markers with type 64 or
65 and the Restart Marker header.

The two RTP/JPEG types currently defined are described below:

						horizontal   vertical   Quantization
	   types  component samp. fact. samp. fact. table number
	 +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	 |       |  1 (Y)  |     2     |     1     |     0     |
	 | 0, 64 |  2 (U)  |     1     |     1     |     1     |
	 |       |  3 (V)  |     1     |     1     |     1     |
	 +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
	 |       |  1 (Y)  |     2     |     2     |     0     |
	 | 1, 65 |  2 (U)  |     1     |     1     |     1     |
	 |       |  3 (V)  |     1     |     1     |     1     |
	 +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+

These sampling factors indicate that the chrominance components of
type 0 video is downsampled horizontally by 2 (often called 4:2:2)
while the chrominance components of type 1 video are downsampled both
horizontally and vertically by 2 (often called 4:2:0).

Types 0 and 1 can be used to carry both progressively scanned and
interlaced image data.  This is encoded using the Type-specific field
in the main JPEG header.  The following values are defined:

  0 : Image is progressively scanned.  On a computer monitor, it can
	  be displayed as-is at the specified width and height.

  1 : Image is an odd field of an interlaced video signal.  The
	  height specified in the main JPEG header is half of the height
	  of the entire displayed image.  This field should be de-
	  interlaced with the even field following it such that lines
	  from each of the images alternate.  Corresponding lines from
	  the even field should appear just above those same lines from
	  the odd field.

  2 : Image is an even field of an interlaced video signal.

  3 : Image is a single field from an interlaced video signal, but
	  it should be displayed full frame as if it were received as
	  both the odd & even fields of the frame.  On a computer
	  monitor, each line in the image should be displayed twice,
	  doubling the height of the image.
				 */

			#endregion

			Type = (packet.Payload.Array[offset++]);

			//Check for a RtpJpeg Type of less than 5 used in RFC2035 for which RFC2435 is the errata
			if (false == rfc2035Quality &&
				Type >= 2 && Type <= 5)
			{
				//Should allow for 2035 decoding seperately
				//Lines of a scan.
				throw new InvalidOperationException("Type numbers 2-5 are reserved and SHOULD NOT be used.  Applications based on RFC 2035 should be updated to indicate the presence of restart markers with type 64 or 65 and the Restart Marker header.");
			}

			if ((Quality = packet.Payload.Array[offset++]) == 0) throw new InvalidOperationException("Quality == 0 is reserved.");

			//Should round?

			//Should use 256 ..with 8 modulo? 227x149 is a good example and is in the jpeg reference

			Width = (ushort)(packet.Payload.Array[offset++] * 8);// in 8 pixel multiples

			//0 values are not specified in the rfc
			if (Width == 0)
			{
				Width = 2040;
			}

			Height = (ushort)(packet.Payload.Array[offset++] * 8);// in 8 pixel multiples

			//0 values are not specified in the rfc
			if (Height == 0)
			{
				Height = 2040;
			}

			//It is worth noting you can send higher resolution pictures may be sent and these values will simply be ignored in such cases or the receiver will have to know to use a 
			//divisor other than 8 to obtain the values when decoding

			/*
			 3.1.3.  Type: 8 bits

			   The type field specifies the information that would otherwise be
			   present in a JPEG abbreviated table-specification as well as the
			   additional JFIF-style parameters not defined by JPEG.  Types 0-63 are
			   reserved as fixed, well-known mappings to be defined by this document
			   and future revisions of this document.  Types 64-127 are the same as
			   types 0-63, except that restart markers are present in the JPEG data
			   and a Restart Marker header appears immediately following the main
			   JPEG header.  Types 128-255 are free to be dynamically defined by a
			   session setup protocol (which is beyond the scope of this document).
			 */
			//Restart Interval 64 - 127
			if (Type > 63 && Type < 128) //Might not need to check Type < 128 but done because of the above statement
			{

				//ProfileHeaderInformation.DataRestartIntervalHeaderSize

				if ((count = end - offset) < 4) throw new InvalidOperationException("Invalid packet.");

				/*
				   This header MUST be present immediately after the main JPEG header
				   when using types 64-127.  It provides the additional information
				   required to properly decode a data stream containing restart markers.

					0                   1                   2                   3
					0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
				   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
				   |       Restart Interval        |F|L|       Restart Count       |
				   +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
				 */
				RestartInterval = Media.Common.Binary.ReadU16(packet.Payload.Array, ref offset, BitConverter.IsLittleEndian);//(ushort)(packet.Payload.Array[offset++] << 8 | packet.Payload.Array[offset++]);

				//Discard first and last bits...
				RestartCount = (ushort)(Media.Common.Binary.ReadU16(packet.Payload.Array, ref offset, BitConverter.IsLittleEndian) & 0x3FFF); //((packet.Payload.Array[offset++] << 8 | packet.Payload.Array[offset++]) & 0x3fff);
			}

			// A Q value of 255 denotes that the  quantization table mapping is dynamic and can change on every frame.
			// Decoders MUST NOT depend on any previous version of the tables, and need to reload these tables on every frame.

			//I check for the buffer position to be 0 because on large images which exceed the size allowed FragmentOffset wraps.
			//Due to my 'updates' [which will soon be seperated from the RFC2435 implementation into another e.g. a new RFC or seperate class.]
			//One cannot use the TypeSpecific field because it's not valid and I have also allowed for TypeSpecific to be set from the StartOfFrame marker to allow:
			//1) Correct component numbering when component numbers do not start at 0 or use non incremental indexes.
			//2) Allow for the SubSampling to be indicated in that same field when not 1x1
			// 2a) For CMYK or RGB one would also need to provide additional data such as Huffman tables and count (the same for Quantization information)

			//Todo,
			//At this point FragmentOffset may have wrapped or been out of order....
			if (FragmentOffset == 0 && Depacketized.Count == 0)
			{

				//RFC2435 http://tools.ietf.org/search/rfc2435#section-3.1.8
				//3.1.8.  Quantization Table header
				/*
				 This header MUST be present after the main JPEG header (and after the
					Restart Marker header, if present) when using Q values 128-255.  It
					provides a way to specify the quantization tables associated with
					this Q value in-band.
				 */
				if (Quality >= (rfc2035Quality ? 100 : 128)) //RFC2035 uses 0->100 where RFC2435 uses 0 ->127 but values 100 - 127 are not specified in the algorithm provided and should possiblly use the alternate quantization tables
				{

					/* http://tools.ietf.org/search/rfc2435#section-3.1.8
					 * Quantization Table Header
					 * -------------------------
					 0                   1                   2                   3
					 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
					+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
					|      MBZ      |   Precision   |             Length            |
					+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
					|                    Quantization Table Data                    |
					|                              ...                              |
					+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
					 */

					//ProfileHeaderInformation.QuantizationTableHeaderSize

					if ((count = end - offset) < 4) throw new InvalidOperationException("Invalid packet.");

					//This can be used to determine incorrectly parsing this data for a RFC2035 packet which does not include a table when the quality is >= 100                            
					if ((packet.Payload.Array[offset]) != 0)
					{
						//Sometimes helpful in determining this...
						//useRfcQuantizer = Quality > 100;

						//offset not moved into what would be the payload

						//create default tables.
						tables = new Media.Common.MemorySegment(CreateQuantizationTables(Type, Quality, PrecisionTable, useRfcQuantizer)); //clamp, maxQ, psycovisual
					}
					else
					{
						//MBZ was just read and is 0
						++offset;

						//Read the PrecisionTable (notes below)
						PrecisionTable = (packet.Payload.Array[offset++]);

						#region RFC2435 Length Field

						/*

								The Length field is set to the length in bytes of the quantization
								table data to follow.  The Length field MAY be set to zero to
								indicate that no quantization table data is included in this frame.
								See section 4.2 for more information.  If the Length field in a
								received packet is larger than the remaining number of bytes, the
								packet MUST be discarded.

								When table data is included, the number of tables present depends on
								the JPEG type field.  For example, type 0 uses two tables (one for
								the luminance component and one shared by the chrominance
								components).  Each table is an array of 64 values given in zig-zag
								order, identical to the format used in a JFIF DQT marker segment.

						 * PrecisionTable *

								For each quantization table present, a bit in the Precision field
								specifies the size of the coefficients in that table.  If the bit is
								zero, the coefficients are 8 bits yielding a table length of 64
								bytes.  If the bit is one, the coefficients are 16 bits for a table
								length of 128 bytes.  For 16 bit tables, the coefficients are
								presented in network byte order.  The rightmost bit in the Precision
								field (bit 15 in the diagram above) corresponds to the first table
								and each additional table uses the next bit to the left.  Bits beyond
								those corresponding to the tables needed by the type in use MUST be
								ignored.

							 */

						#endregion

						//Length of all tables
						ushort Length = Media.Common.Binary.ReadU16(packet.Payload.Array, ref offset, BitConverter.IsLittleEndian); //(ushort)(packet.Payload.Array[offset++] << 8 | packet.Payload.Array[offset++]);

						//If there is Table Data Read it from the payload, Length should never be larger than 128 * tableCount
						if (Length == 0 && Quality == byte.MaxValue/* && false == allowQualityLengthException*/) throw new InvalidOperationException("RtpPackets MUST NOT contain Q = 255 and Length = 0.");
						else if (Length == 0 || Length > end - offset)
						{
							//If the indicated length is greater than that of the packet taking into account the offset and padding
							//Or
							//The length is 0

							//Use default tables
							tables = new Media.Common.MemorySegment(CreateQuantizationTables(Type, Quality, PrecisionTable, useRfcQuantizer));
						}

						//Copy the tables present
						tables = new Media.Common.MemorySegment(packet.Payload.Array, offset, (int)Length);

						offset += (int)Length;
					}
				}
				else // Create them from the given Quality parameter
				{
					tables = new Media.Common.MemorySegment(CreateQuantizationTables(Type, Quality, PrecisionTable, useRfcQuantizer));
				}

				//Potentially make instance level properties for the tables so they can be accessed again easily.

				depacketized = new Media.Common.MemorySegment(CreateJPEGHeaders(TypeSpecific, Type, Width, Height, tables, PrecisionTable, RestartInterval));

				//Generate the JPEG Header after reading or generating the QTables
				//Ensure always at the first index of the Depacketized list. (FragmentOffset - 1)
				Depacketized.Add(packetKey - 1, depacketized);

				//tables.Dispose();
				//tables = null;
			}

			//If there is no more data in the payload then the data which needs to be checked in already in the Depacketized list or assigned.
			if ((count = end - offset) > 0)
			{
				//Store the added segment to check for the EOI
				depacketized = new Media.Common.MemorySegment(packet.Payload.Array, offset, count);

				//Add the data which is depacketized
				Depacketized.Add(packetKey++, depacketized);
			}

			//When the marker is present it indicates it is the last packet related to the frame.
			if (packet.Marker)
			{
				//Get the last value added if depacketized was not already assigned.
				if (depacketized == null) depacketized = Depacketized.Values.Last();

				//Check for EOI and if note present Add it at the FragmentOffset + 1
				if (depacketized.Array[depacketized.Count - 2] != Media.Codecs.Image.Jpeg.Markers.EndOfInformation)
				{
					Depacketized.Add(packetKey++, EndOfInformationMarkerSegment);
				}
			}

			//depacketized = null;
		}

		static readonly Media.Common.MemorySegment EndOfInformationMarkerSegment = new Media.Common.MemorySegment(new byte[] { Media.Codecs.Image.Jpeg.Markers.Prefix, Media.Codecs.Image.Jpeg.Markers.EndOfInformation });

		/// <summary>
		/// http://en.wikipedia.org/wiki/JPEG_File_Interchange_Format
		/// </summary>
		/// <param name="jpegType"></param>
		/// <param name="width"></param>
		/// <param name="height"></param>
		/// <param name="tables"></param>
		/// <param name="precision"></param>
		/// <param name="dri"></param>
		/// <returns></returns>
		internal static byte[] CreateJPEGHeaders(byte typeSpec, byte jpegType, uint width, uint height, Media.Common.MemorySegment tables, byte precision, ushort dri) //bool jfif
		{
			List<byte> result = new List<byte>();

			int tablesCount = tables.Count;

			result.Add(Media.Codecs.Image.Jpeg.Markers.Prefix);
			result.Add(Media.Codecs.Image.Jpeg.Markers.StartOfInformation);//SOI      

			//JFIF marker should be included here if jfif header is required. (16 more bytes)

			//JFXX marker would have thumbnail but is not required

			//Quantization Tables (if needed, pass an empty tables segment to omit)
			if (tables.Count > 0) result.AddRange(CreateQuantizationTableMarkers(tables, precision));

			//Data Restart Invertval
			if (dri > 0) result.AddRange(CreateDataRestartIntervalMarker(dri));

			//Start Of Frame

			/*
			   BitsPerSample / ColorComponents (1)
			   EncodingProcess	(1)
			 * Possible Values
					0xc0 = Baseline DCT, Huffman coding 
					0xc1 = Extended sequential DCT, Huffman coding 
					0xc2 = Progressive DCT, Huffman coding 
					0xc3 = Lossless, Huffman coding 
			 *      0xc4 = Huffman Table.
					0xc5 = Sequential DCT, differential Huffman coding 
					0xc6 = Progressive DCT, differential Huffman coding 
					0xc7 = Lossless, Differential Huffman coding 
			 *      0xc8 = Extension
					0xc9 = Extended sequential DCT, arithmetic coding 
					0xca = Progressive DCT, arithmetic coding 
					0xcb = Lossless, arithmetic coding 
			 *      0xcc =  DAC   = 0xcc,   define arithmetic-coding conditioning
					0xcd = Sequential DCT, differential arithmetic coding 
					0xce = Progressive DCT, differential arithmetic coding 
					0xcf = Lossless, differential arithmetic coding
			 *      0xf7 = JPEG-LS Start Of Frame
				ImageHeight	(2)
				ImageWidth	(2) 
				YCbCrSubSampling	(1)
			 * Possible Values
					'1 1' = YCbCr4:4:4 (1 1) 
					'1 2' = YCbCr4:4:0 (1 2) 
					'1 4' = YCbCr4:4:1 (1 4) 
					'2 1' = YCbCr4:2:2 (2 1) 
					'2 2' = YCbCr4:2:0 (2 2) 
					'2 4' = YCbCr4:2:1 (2 4) 
					'4 1' = YCbCr4:1:1 (4 1) 
					'4 2' = YCbCr4:1:0 (4 2)
			 */

			//Need a progrssive indication, problem is that CMYK and RGB also use that indication
			bool progressive = false; /* = typeSpec == Media.Codecs.Image.Jpeg.Markers.StartOfProgressiveFrame;
                if (progressive) typeSpec = 0;*/

			result.Add(Media.Codecs.Image.Jpeg.Markers.Prefix);

			//This is not soley based on progressive or not, this needs to include more types based on what is defined (above)
			if (progressive)
				result.Add(Media.Codecs.Image.Jpeg.Markers.StartOfProgressiveFrame);//SOF
			else
				result.Add(Media.Codecs.Image.Jpeg.Markers.StartOfBaselineFrame);//SOF

			//Todo properly build headers?
			//If only 1 table (AND NOT PROGRESSIVE)
			if (tablesCount == 64 && false == progressive)
			{
				result.Add(0x00); //Length
				result.Add(0x0b); //
				result.Add(0x08); //Bits Per Components and EncodingProcess

				result.Add((byte)(height >> 8)); //Height
				result.Add((byte)height);

				result.Add((byte)(width >> 8)); //Width
				result.Add((byte)width);

				result.Add(0x01); //Number of components
				result.Add(0x00); //Component Number
				result.Add((byte)(typeSpec > 0 ? typeSpec : 0x11)); //Horizontal Sampling Factor
				result.Add(0x00); //Matrix Number
			}
			else
			{
				result.Add(0x00); //Length
				result.Add(0x11); // Decimal 17 -> 15 bytes
				result.Add(0x08); //Bits Per Components and EncodingProcess

				result.Add((byte)(height >> 8)); //Height
				result.Add((byte)height);

				result.Add((byte)(width >> 8)); //Width
				result.Add((byte)width);

				result.Add(0x03);//Number of components

				result.Add(0x01);//Component Number

				//Set the Horizontal Sampling Factor
				result.Add((byte)((jpegType & 1) == 0 ? 0x21 : 0x22));

				result.Add(0x00);//Matrix Number (Quant Table Id)?
				result.Add(0x02);//Component Number

				result.Add((byte)(typeSpec > 0 ? typeSpec : 0x11));//Horizontal or Vertical Sample 

				result.Add((byte)(tablesCount == 64 ? 0x00 : 0x01));//Matrix Number

				result.Add(0x03);//Component Number
				result.Add((byte)(typeSpec > 0 ? typeSpec : 0x11));//Horizontal or Vertical Sample

				result.Add((byte)(tablesCount == 64 ? 0x00 : 0x01));//Matrix Number      
			}

			//Huffman Tables, Check for progressive version?

			if (progressive)
			{
				result.AddRange(CreateHuffmanTableMarker(lum_dc_codelens_p, lum_dc_symbols_p, 0, 0));
				result.AddRange(CreateHuffmanTableMarker(chm_dc_codelens_p, chm_dc_symbols_p, 1, 0));
			}
			else
			{
				result.AddRange(CreateHuffmanTableMarker(lum_dc_codelens, lum_dc_symbols, 0, 0));
				result.AddRange(CreateHuffmanTableMarker(lum_ac_codelens, lum_ac_symbols, 0, 1));
			}


			//More then 1 table (AND NOT PROGRESSIVE)
			if (tablesCount > 64 && false == progressive)
			{
				result.AddRange(CreateHuffmanTableMarker(chm_dc_codelens, chm_dc_symbols, 1, 0));
				result.AddRange(CreateHuffmanTableMarker(chm_ac_codelens, chm_ac_symbols, 1, 1));
			}

			//Start Of Scan
			result.Add(Media.Codecs.Image.Jpeg.Markers.Prefix);
			result.Add(Media.Codecs.Image.Jpeg.Markers.StartOfScan);//Marker SOS

			//If only 1 table (AND NOT PROGRESSIVE)
			if (tablesCount == 64)
			{
				result.Add(0x00); //Length
				result.Add(0x08); //Length - 12
				result.Add(0x01); //Number of components
				result.Add(0x00); //Component Number
				result.Add(0x00); //Matrix Number

			}
			else
			{
				result.Add(0x00); //Length
				result.Add(0x0c); //Length - 12
				result.Add(0x03); //Number of components
				result.Add(0x01); //Component Number
				result.Add(0x00); //Matrix Number

				//Should be indicated from typeSpec...

				result.Add(0x02); //Component Number
				result.Add((byte)(progressive ? 0x10 : 0x11)); //Horizontal or Vertical Sample

				result.Add(0x03); //Component Number
				result.Add((byte)(progressive ? 0x10 : 0x11)); //Horizontal or Vertical Sample
			}


			if (progressive)
			{
				result.Add(0x00); //Start of spectral
				result.Add(0x00); //End of spectral
				result.Add(0x01); //Successive approximation bit position (high, low)
			}
			else
			{
				result.Add(0x00); //Start of spectral
				result.Add(0x3f); //End of spectral (63)
				result.Add(0x00); //Successive approximation bit position (high, low)
			}

			return result.ToArray();
		}

		internal static byte[] CreateDataRestartIntervalMarker(ushort dri)
		{
			return new byte[] { Media.Codecs.Image.Jpeg.Markers.Prefix, Media.Codecs.Image.Jpeg.Markers.DataRestartInterval, 0x00, 0x04, (byte)(dri >> 8), (byte)(dri) };
		}

		//Todo, move to JPEG.
		internal static byte[] CreateHuffmanTableMarker(byte[] codeLens, byte[] symbols, int tableNo, int tableClass)
		{
			List<byte> result = new List<byte>();
			result.Add(Media.Codecs.Image.Jpeg.Markers.Prefix);
			result.Add(Media.Codecs.Image.Jpeg.Markers.HuffmanTable);
			result.Add(0x00); //Legnth
			result.Add((byte)(3 + codeLens.Length + symbols.Length)); //Length
			result.Add((byte)((tableClass << 4) | tableNo)); //Id
			result.AddRange(codeLens);//Data
			result.AddRange(symbols);
			return result.ToArray();
		}

		/// <summary>
		/// Creates a Jpeg QuantizationTableMarker for each table given in the tables
		/// The precision must be the same for all tables when using this function.
		/// </summary>
		/// <param name="tables">The tables verbatim, either 1 or 2 (Lumiance and Chromiance)</param>
		/// <param name="precisionTable">The byte which indicates which table has 16 bit coeffecients</param>
		/// <returns>The table with marker and prefix and Pq/Tq byte</returns>
		internal static byte[] CreateQuantizationTableMarkers(Media.Common.MemorySegment tables, byte precisionTable)
		{
			//List<byte> result = new List<byte>();

			int tableCount = tables.Count / (precisionTable > 0 ? 128 : 64);

			//Invalid sized tables....
			if (tables.Count % tableCount > 0) tableCount = 1;

			//??Some might have more then 3?
			if (tableCount > 3) throw new ArgumentOutOfRangeException("tableCount");

			int tableSize = tables.Count / tableCount;

			//The len includes the 2 bytes for the length and a single byte for the Lqcd
			byte len = (byte)(tableSize + 3);

			//Each tag is 4 bytes (prefix and tag) + 2 for len = 4 + 1 for Precision and TableId 
			byte[] result = new byte[(5 * tableCount) + (tableSize * tableCount)];

			//1 Table

			//Define QTable
			result[0] = Media.Codecs.Image.Jpeg.Markers.Prefix;
			result[1] = Media.Codecs.Image.Jpeg.Markers.QuantizationTable;

			result[2] = 0;//Len
			result[3] = len;

			//Pq / Tq
			result[4] = (byte)(precisionTable << 8 > 0 ? 8 : 0); // Precision and table (id 0 filled by shift)

			//First table. Type - Lumiance usually when two
			System.Array.Copy(tables.Array, tables.Offset, result, 5, tableSize);

			//2 Tables
			if (tableCount > 1)
			{
				result[tableSize + 5] = Media.Codecs.Image.Jpeg.Markers.Prefix;
				result[tableSize + 6] = Media.Codecs.Image.Jpeg.Markers.QuantizationTable;

				result[tableSize + 7] = 0;//Len LSB
				result[tableSize + 8] = len;

				//Pq / Tq
				result[tableSize + 9] = (byte)(precisionTable << 7 > 0 ? 8 : 1);//Precision and table Id 1

				//Second Table. Type - Chromiance usually when two
				System.Array.Copy(tables.Array, tables.Offset + tableSize, result, 10 + tableSize, tableSize);
			}

			//3 Tables
			if (tableCount > 2)
			{
				result[tableSize + 10] = Media.Codecs.Image.Jpeg.Markers.Prefix;
				result[tableSize + 11] = Media.Codecs.Image.Jpeg.Markers.QuantizationTable;

				result[tableSize + 12] = 0;//Len LSB
				result[tableSize + 13] = len;

				//Pq / Tq
				result[tableSize + 14] = (byte)(precisionTable << 6 > 0 ? 8 : 2);//Precision and table Id 2

				//Second Table. Type - Chromiance usually when two
				System.Array.Copy(tables.Array, tables.Offset + tableSize, result, 14 + tableSize, tableSize);
			}

			return result;
		}


		static byte[] lum_dc_codelens = { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 },
					//Progressive
					lum_dc_codelens_p = { 0, 2, 3, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

		static byte[] lum_dc_symbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
					//Progressive
					lum_dc_symbols_p = { 0, 2, 3, 0, 1, 4, 5, 6, 7 }; //lum_dc_symbols_p = { 0, 0, 2, 1, 3, 4, 5, 6, 7}; Work for TestProg but not TestImgP

		//JpegHuffmanTable StdACLuminance

		static byte[] lum_ac_codelens = { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d };

		static byte[] lum_ac_symbols =
		{
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
				0xf9, 0xfa
			};

		//Chromiance

		//JpegHuffmanTable StdDCChrominance
		static byte[] chm_dc_codelens = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 },
					//Progressive
					chm_dc_codelens_p = { 0, 3, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

		static byte[] chm_dc_symbols = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
					//Progressive
					chm_dc_symbols_p = { 0, 1, 2, 3, 0, 4, 5 };

		//JpegHuffmanTable StdACChrominance

		static byte[] chm_ac_codelens = { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 };

		static byte[] chm_ac_symbols =
		{
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
				0xf9, 0xfa
			};

		// The default 'luma' and 'chroma' quantizer tables, in zigzag order and energy reduced
		static byte[] defaultQuantizers = new byte[]
	{
           // luma table: Psychovisual
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

		static byte[] rfcQuantizers = new byte[]
	{
           // luma table:
            //From RFC2435 / Jpeg Spec
            16, 11, 10, 16, 24, 40, 51, 61,
			12, 12, 14, 19, 26, 58, 60, 55,
			14, 13, 16, 24, 40, 57, 69, 56,
			14, 17, 22, 29, 51, 87, 80, 62,
			18, 22, 37, 56, 68, 109, 103, 77,
			24, 35, 55, 64, 81, 104, 113, 92,
			49, 64, 78, 87, 103, 121, 120, 101,
			72, 92, 95, 98, 112, 100, 103, 99,
           // chroma table:
            //From RFC2435 / Jpeg Spec
            17, 18, 24, 47, 99, 99, 99, 99,
			18, 21, 26, 66, 99, 99, 99, 99,
			24, 26, 56, 99, 99, 99, 99, 99,
			47, 66, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99,
			99, 99, 99, 99, 99, 99, 99, 99
	};

		//http://www.jatit.org/volumes/Vol70No3/24Vol70No3.pdf
		static byte[] psychoVisualQuantizers = new byte[]
	{
           // luma table:
           16, 14, 13, 15, 19, 28, 37, 55,
		   14, 13, 15, 19, 28, 37, 55, 64,
		   13, 15, 19, 28, 37, 55, 64, 83,
		   15, 19, 28, 37, 55, 64, 83, 103,
		   19, 28, 37, 55, 64, 83, 103, 117,
		   28, 37, 55, 64, 83, 103, 117, 117,
		   37, 55, 64, 83, 103, 117, 117, 111,
		   55, 64, 83, 103, 117, 117, 111, 90,
           //chroma table
           18, 18, 23, 34, 45, 61, 71, 9,
		   18, 23, 34, 45, 61, 71, 92, 92,
		   23, 34, 45, 61, 71, 92, 92, 104,
		   34, 45, 61, 71, 92, 92, 104, 115,
		   45, 61, 71, 92, 92, 104, 115, 119,
		   61, 71, 92, 92, 104, 115, 119, 112,
		   71, 92, 92, 104, 115, 119, 112, 106,
		   92, 92, 104, 115, 119, 112, 106, 100
	};

		/// <summary>
		/// Creates a Luma and Chroma Table in ZigZag order using the default quantizers specified in RFC2435
		/// </summary>
		/// <param name="type">Should be used to determine the sub sambling and table count or atleast which table to create? (currently not used)</param>
		/// <param name="Q">The quality factor</param>            
		/// <param name="precision"></param>
		/// <param name="useRfcQuantizer"></param>
		/// <returns>luma and chroma tables</returns>
		internal static byte[] CreateQuantizationTables(uint type, uint Q, byte precision, bool useRfcQuantizer, bool clamp = true, int maxQ = 100, bool psychoVisualQuantizer = false)
		{
			//Ensure not the reserved value.
			if (Q == 0) throw new InvalidOperationException("Q == 0 is reserved.");

			//RFC2035 did not specify a quantization table header and uses the values 0 - 127 to define this.
			//RFC2035 also does not specify what to do with Quality values 100 - 127
			//RFC2435 Other values [between 1 and 99 inclusive but] less than 128 are reserved

			//As per RFC2435 4.2.
			//if (Q >= 100) throw new InvalidOperationException("Q >= 100, a dynamically defined quantization table is used, which might be specified by a session setup protocol.");

			byte[] quantizer = useRfcQuantizer ? rfcQuantizers : psychoVisualQuantizer ? psychoVisualQuantizers : defaultQuantizers;

			//This is because Q can be 1 - 128 and values 100 - 127 may produce different Seed values however the standard only defines for Q 1 => 100
			//The higher values sometimes round or don't depending on the system they were generated in or the decoder of the system and are typically found in progressive images.

			//Note that FFMPEG uses slightly different quantization tables (as does this implementation) which are saturated for viewing within the psychovisual threshold.


			//Factor restricted to range of 1 and 100 (or maxQ)
			int factor = (int)(clamp ? Media.Common.Binary.Clamp(Q, 1, maxQ) : Q);

			// 4.2 Text
			// S = 5000 / Q          for  1 <= Q <= 50
			//   = 200 - 2 * Q       for 51 <= Q <= 99

			//Seed quantization value for values less than or equal to 50, ffmpeg uses 1 - 49... @ https://ffmpeg.org/doxygen/2.3/rtpdec__jpeg_8c_source.html
			//Following the RFC @ Appendix A https://tools.ietf.org/html/rfc2435#appendix-A

			//This implementation differs slightly in that it uses the text from 4.2 literally.
			int q = (Q <= 50 ? (int)(5000 / factor) : 200 - factor * 2);

			//Create 2 quantization tables from Seed quality value using the RFC quantizers
			int tableSize = (precision > 0 ? 128 : 64);/// quantizer.Length / 2;

			//The tableSize should depend on the bit in the precision table.
			//This implies that the count of tables must be given... or that the math determining this needs to be solid..
			byte[] resultTables = new byte[tableSize * 2]; //two tables being returned... (should allow for only 1?)

			//bool luma16 = Common.Binary.GetBit(precision, 0), chroma16 = Common.Binary.GetBit(precision, 1);

			//Iterate for each element in the tableSize (the default quantizers are 64 bytes each in 8 bit form)

			int destLuma = 0, destChroma = 128;

			for (int lumaIndex = 0, chromaIndex = 64; lumaIndex < 64; ++lumaIndex, ++chromaIndex)
			{
				//Check the bit in the precision table for the value which indicates if the tables are 16 bit or 32 bit?
				//Normally, it would be read from the precision Byte when decoding but because of how this function is called 
				//the value for precision as given is probably applicable for both tables because having a mixed set of 8 and 16 bit tables is not very likley
				//Would need to refactor to write luma and then write chroma incase one is 16 bit and the other is not..... not very likely

				//8 Bit tables       
				if (precision == 0)
				{
					//Clamp with Min, Max (Should be written in correct bit order)
					//Luma
					resultTables[lumaIndex] = (byte)Media.Common.Binary.Min(Media.Common.Binary.Max((quantizer[lumaIndex] * q + 50) / 100, 1), byte.MaxValue);

					//Chroma
					resultTables[chromaIndex] = (byte)Media.Common.Binary.Min(Media.Common.Binary.Max((quantizer[chromaIndex] * q + 50) / 100, 1), byte.MaxValue);
				}
				else //16 bit tables
				{

					//Using the 8 bit table offset create the value and copy it to its 16 bit offset

					//Luma
					if (BitConverter.IsLittleEndian)
						BitConverter.GetBytes(Media.Common.Binary.ReverseU16((ushort)Media.Common.Binary.Min(Media.Common.Binary.Max((quantizer[lumaIndex] * q + 50) / 100, 1), byte.MaxValue))).CopyTo(resultTables, destLuma);
					else
						BitConverter.GetBytes((ushort)Media.Common.Binary.Min(Math.Max((quantizer[lumaIndex] * q + 50) / 100, 1), byte.MaxValue)).CopyTo(resultTables, destLuma);

					destLuma += 2;

					//Chroma
					if (BitConverter.IsLittleEndian)
						BitConverter.GetBytes(Media.Common.Binary.ReverseU16((ushort)Media.Common.Binary.Min(Media.Common.Binary.Max((quantizer[chromaIndex] * q + 50) / 100, 1), byte.MaxValue))).CopyTo(resultTables, destChroma);
					else
						BitConverter.GetBytes((ushort)Media.Common.Binary.Min(Media.Common.Binary.Max((quantizer[chromaIndex] * q + 50) / 100, 1), byte.MaxValue)).CopyTo(resultTables, destChroma);

					destChroma += 2;
				}
			}

			return resultTables;
		}




	}
}
