﻿#region Copyright
/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://net7mma.codeplex.com/
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. http://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */
#endregion

#region Using Statements

using System;
using System.Collections.Generic;
using System.Linq;
using Media.Common;

#endregion

namespace Media.Rtp
{

    #region RtpPacket

    /// <summary>
    /// A managed implemenation of the Rtp abstraction found in RFC3550.
    /// <see cref="http://tools.ietf.org/html/rfc3550"> RFC3550 </see> for more information
    /// </summary>
    public class RtpPacket : BaseDisposable, IPacket, ICloneable
    {
        #region Fields
        /// Provides a storage location for bytes which are owned by this instance.
        /// </summary>
        byte[] m_OwnedOctets;
        /// The RtpHeader assoicated with this RtpPacket instance.
        /// </summary>
        /// <remarks>
        /// readonly attempts to ensure no race conditions when accessing this field e.g. during property access when using the Dispose method.
        /// </remarks>
        public readonly RtpHeader Header;
        /// The binary data of the RtpPacket which may contain a ContributingSourceList, RtpExtension and Padding.
        /// </summary>
        public MemorySegment Payload { get; protected set; }
        /// Determines the amount of unsigned integers which must be contained in the ContributingSourcesList to make the payload complete.
        /// <see cref="RtpHeader.ContributingSourceCount"/>
        /// </summary>
        /// <remarks>
        /// Obtained by performing a Multiply against 4 from the high quartet in the first octet of the RtpHeader.
        /// This number can never be larger than 60 given by the mask `0x0f` (15) used to obtain the ContributingSourceCount.
        /// Subsequently >15 * 4  = 60
        /// Clamped with Min(60, Max(0, N)) where N = ContributingSourceCount * 4;
        /// </remarks>
        public int ContributingSourceListOctets
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        }
        /// Determines the amount of octets in the RtpExtension in this RtpPacket.
        /// The maximum value this property can return is 65535.
        /// <see cref="RtpExtension.LengthInWords"/> for more information.
        /// </summary>
        public int ExtensionOctets
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        }
        /// The amount of octets which should exist in the payload and belong either to the SourceList and or the RtpExtension.
        /// This amount does not reflect any padding which may be present because the padding is at the end of the payload.
        /// </summary>
        public int HeaderOctets
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        }
        /// Gets the amount of octets which are in the Payload property which are part of the padding if IsComplete is true.            
        /// This property WILL return the value of the last non 0 octet in the payload if Header.Padding is true, otherwise 0.
        /// <see cref="RFC3550.ReadPadding"/> for more information.
        /// </summary>
        public int PaddingOctets
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        }
        /// Indicates if the RtpPacket is formatted in a complaince to RFC3550 and that all data required to read the RtpPacket is available.
        /// This is dertermined by performing checks against the RtpHeader and data in the Payload to validate the SouceList and Extension if present.
        /// <see cref="SourceList"/> and <see cref="RtpExtension"/> for further information.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                int octetsContained = Payload.Count;
                octetsContained -= ContributingSourceListOctets;
                if (octetsContained < 0) return false;
                if (Header.Extension) using (RtpExtension extension = GetExtension())
                    {
                        if (extension == null || false == extension.IsComplete) return false;
                        octetsContained -= extension.Size;
                    }
                if (false == Header.Padding) return octetsContained >= 0;
                int paddingOctets = PaddingOctets;
                return octetsContained >= paddingOctets;
            }
        }
        /// Indicates the length in bytes of this RtpPacket instance. (Including the RtpHeader as well as SourceList and Extension if present.)
        /// </summary>
        public int Length
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        }
        /// <see cref="PayloadData"/>
        /// </summary>
        internal protected Common.MemorySegment PayloadDataSegment
        {
            get
            {
                //Proably don't have to check...
                if (IsDisposed || Payload.Count == 0) return Media.Common.MemorySegment.Empty;
            }
        }
        /// Gets the data in the Payload which does not belong to the ContributingSourceList or RtpExtension or Padding.
        /// The data if present usually contains data related to signal codification,
        /// the coding of which can be determined by a combination of the PayloadType and SDP information which was used to being the participation 
        /// which resulted in the transfer of this RtpPacket instance.
        /// </summary>
        public IEnumerable<byte> PayloadData
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        }
        {
            get
            {
                //Maybe should provide the incomplete data...
                if (IsDisposed || false == IsComplete) return Media.Common.MemorySegment.Empty;
            }
        }
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        }
        /// Copies the given octets to the Payload before any Padding and calls <see cref="SetLengthInWordsMinusOne"/>.
        /// </summary>
        /// <param name="octets">The octets to add</param>
        /// <param name="offset">The offset to start copying</param>
        /// <param name="count">The amount of bytes to copy</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        internal protected virtual void AddBytesToPayload(IEnumerable<byte> octets, int offset = 0, int count = int.MaxValue)
        {
            if (IsReadOnly) throw new InvalidOperationException("Can only set the AddBytesToPayload when IsReadOnly is false.");
            if (Padding)
            {
                //Determine the amount of bytes in the payload
                int payloadCount = Payload.Count,
                    //Determine the padding octets offset
                    paddingOctets = PaddingOctets,
                    //Determine the amount of octets in the payload
                    payloadOctets = payloadCount - paddingOctets;
                m_OwnedOctets = Enumerable.Concat(m_OwnedOctets.Take(m_OwnedOctets.Length - paddingOctets), Enumerable.Concat(octets.Skip(offset).Take(newBytes), Payload.Skip(payloadOctets).Take(paddingOctets))).ToArray();
            else if (m_OwnedOctets == null)
            {
                m_OwnedOctets = octets.Skip(offset).Take(newBytes).ToArray();
            }
            else
            {
                m_OwnedOctets = Enumerable.Concat(m_OwnedOctets, octets.Skip(offset).Take(newBytes)).ToArray();
            }
            return;
        }
        public RtpPacket(int version, bool padding, bool extension, byte[] payload)
            : this(new RtpHeader(version, padding, extension), payload)
        {
        public RtpPacket(int version, bool padding, bool extension, bool marker, int payloadType, int csc, int ssrc, int seq, int timestamp, byte[] payload = null)
            : this(new RtpHeader(version, padding, extension, marker, payloadType, csc, ssrc, seq, timestamp), payload)
        {
            
        }
        /// Creates a RtpPacket instance by projecting the given sequence to an array which is subsequently owned by the instance.
        /// </summary>
        /// <param name="header">The header to utilize. When Dispose is called this header will be diposed if ownsHeader is true.</param>
        /// <param name="octets">The octets to project</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public RtpPacket(RtpHeader header, IEnumerable<byte> octets, bool shouldDispose = true) : base(shouldDispose)
        {
            if (header == null) throw new ArgumentNullException("header");
            Header = header;
            m_OwnedOctets = (octets ?? Common.MemorySegment.Empty).ToArray();
            Payload = new MemorySegment(m_OwnedOctets, 0, m_OwnedOctets.Length);
        }
        /// Creates a RtpPacket instance from an existing RtpHeader and payload.
        /// Check the IsValid property to see if the RtpPacket is well formed.
        /// </summary>
        /// <param name="header">The existing RtpHeader</param>
        /// <param name="payload">The data contained in the payload</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public RtpPacket(RtpHeader header, MemorySegment payload, bool shouldDispose = true) : base(shouldDispose)
        {
            if (header == null) throw new ArgumentNullException("header");
        }
        /// Creates a RtpPacket instance by copying data from the given buffer at the given offset.
        /// </summary>
        /// <param name="buffer">The buffer which contains the binary RtpPacket to decode</param>
        /// <param name="offset">The offset to start copying</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public RtpPacket(byte[] buffer, int offset, int count, bool shouldDispose = true) : base(shouldDispose)
        {
            if (buffer == null || buffer.Length == 0 || count <= 0) throw new ArgumentException("Must have data in a RtpPacket");
            Header = new RtpHeader(new Common.MemorySegment(m_OwnedOctets, offset, count));
            {
                //Create a segment to the payload deleniated by the given offset and the constant Length of the RtpHeader.
                Payload = new MemorySegment(m_OwnedOctets, RtpHeader.Length, count - RtpHeader.Length);
            }
            else
            {                
                //m_OwnedOctets = Media.Common.MemorySegment.EmptyBytes; //IsReadOnly should be false
                //Payload = new MemoryReference(m_OwnedOctets, 0, 0, m_OwnsHeader);
                Payload = MemorySegment.Empty;
            }
        }
        public RtpPacket(byte[] buffer, int offset, bool shouldDispose = true) : this(buffer, offset, buffer.Length - offset, shouldDispose) { }
        /// Creates a packet instance of the given size.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="shouldDispose"></param>
        public RtpPacket(int size, bool shouldDispose = true) : base(shouldDispose)
        {
            size = Common.Binary.Max(0, size);
        }
            
        #endregion
        /// <see cref="RtpHeader.Version"/>
        /// </summary>
        public int Version
        {
            get { return Header.Version; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("Version can only be set when IsReadOnly is false.");
                Header.Version = value;
            }
        }
        /// <see cref="RtpHeader.Padding"/>
        /// </summary>
        public bool Padding
        {
            get { return Header.Padding; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("Padding can only be set when IsReadOnly is false.");
                Header.Padding = value;
            }
        }
        /// <see cref="RtpHeader.Extension"/>
        /// </summary>
        public bool Extension
        {
            get { return Header.Extension; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("Extension can only be set when IsReadOnly is false.");
                Header.Extension = value;
            }
        }
        /// <see cref="RtpHeader.Marker"/>
        /// </summary>
        public bool Marker
        {
            get { return Header.Marker; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("Marker can only be set when IsReadOnly is false.");
                Header.Marker = value;
            }
        }
        /// <see cref="RtpHeader.ContributingSourceCount"/>
        /// </summary>
        public int ContributingSourceCount
        {
            get { return Header.ContributingSourceCount; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("ContributingSourceCount can only be set when IsReadOnly is false.");
                Header.ContributingSourceCount = value;
            }
        }
        /// <see cref="RtpHeader.PayloadType"/>
        /// </summary>
        public int PayloadType
        {
            get { return Header.PayloadType; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("PayloadType can only be set when IsReadOnly is false.");
                Header.PayloadType = value;
            }
        }
        /// <see cref="RtpHeader.SequenceNumber"/>
        /// </summary>
        public int SequenceNumber
        {
            get { return Header.SequenceNumber; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("SequenceNumber can only be set when IsReadOnly is false.");
                Header.SequenceNumber = value;
            }
        }
        /// <see cref="RtpHeader.Timestamp"/>
        /// </summary>
        public int Timestamp 
        {
            get { return Header.Timestamp; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("Timestamp can only be set when IsReadOnly is false.");
                Header.Timestamp = value;
            }
        }
        /// <see cref="RtpHeader.SynchronizationSourceIdentifier"/>
        /// </summary>
        public int SynchronizationSourceIdentifier
        {
            get { return Header.SynchronizationSourceIdentifier; }
            internal protected set
            {
                if (IsReadOnly) throw new InvalidOperationException("SynchronizationSourceIdentifier can only be set when IsReadOnly is false.");
                Header.SynchronizationSourceIdentifier = value;
            }
        }
        /// Gets an Enumerator which can be used to read the contribuing sources contained in this RtpPacket.
        /// <see cref="SourceList"/> for more information.
        /// </summary>
        public Media.RFC3550.SourceList GetSourceList() { if (IsDisposed) return null; return new Media.RFC3550.SourceList(this); }
        /// Gets the RtpExtension which would be created as a result of reading the data from the RtpPacket's payload which would be contained after any contained ContributingSourceList.
        /// If the RtpHeader does not have the Extension bit set then null will be returned.
        /// <see cref="RtpHeader.Extension"/> for more information.
        /// </summary>
        [CLSCompliant(false)]
        public RtpExtension GetExtension()
        {
            return false == IsDisposed && Header.Extension && (Payload.Count - ContributingSourceListOctets) > RtpExtension.MinimumSize ? new RtpExtension(this) : null;
        }
        /// Provides the logic for cloning a RtpPacket instance.
        /// The RtpPacket class does not have a Copy Constructor because of the variations in which a RtpPacket can be cloned.
        /// </summary>
        /// <param name="includeSourceList">Indicates if the SourceList should be copied.</param>
        /// <param name="includeExtension">Indicates if the Extension should be copied.</param>
        /// <param name="includePadding">Indicates if the Padding should be copied.</param>
        /// <param name="selfReference">Indicates if the new instance should reference the data contained in this instance.</param>
        /// <returns>The RtpPacket cloned as result of calling this function</returns>
        public RtpPacket Clone(bool includeSourceList, bool includeExtension, bool includePadding, bool includeCoeffecients, bool selfReference)
        {
            //If the sourcelist and extensions are to be included and selfReference is true then return the new instance using the a reference to the data already contained.
            if (includeSourceList && includeExtension && includePadding && includeCoeffecients) return selfReference ? new RtpPacket(Header, Payload, false) { Transferred = Transferred } : new RtpPacket(Prepare().ToArray(), 0) { Transferred = Transferred };
            if (includeSourceList && hasSourceList)
            {
                var sourceList = GetSourceList();
                if (sourceList != null)
                {
                    binarySequence = GetSourceList().GetBinaryEnumerable();
                }
                else binarySequence = Media.Common.MemorySegment.EmptyBytes;
            }
            bool hasExtension = Header.Extension;
            if (hasExtension && includeExtension)
            {
                //Get the Extension
                using (RtpExtension extension = GetExtension())
                {
                    //If an extension could be obtained include it
                    if (extension != null) binarySequence = binarySequence.Concat(extension);
                }
            }
            if (includeCoeffecients) binarySequence = binarySequence.Concat(PayloadData); //Add the binary data to the packet except any padding
            bool hasPadding = Header.Padding;
            if (hasPadding && includePadding) binarySequence = binarySequence.Concat(Payload.Array.Skip(Payload.Offset + Payload.Count - PaddingOctets)); //If just the padding is required the skip the Coefficients
            return new RtpPacket(new RtpHeader(Header.Version, includePadding && hasPadding, includeExtension && hasExtension)
            {
                Timestamp = Header.Timestamp,
                SequenceNumber = Header.SequenceNumber,
                SynchronizationSourceIdentifier = Header.SynchronizationSourceIdentifier,
                PayloadType = Header.PayloadType,
                ContributingSourceCount = includeSourceList ? Header.ContributingSourceCount : 0
            }.Concat(binarySequence).ToArray(), 0) { Transferred = Transferred };
        }
        /// Generates a sequence of bytes containing the RtpHeader and any data contained in Payload.
        /// (Including the SourceList and RtpExtension if present)
        /// </summary>
        /// <param name="other">The optional other RtpHeader to utilize in the preperation</param>
        /// <returns>The sequence created.</returns>
        public IEnumerable<byte> Prepare(RtpHeader other = null)
        {
            return Enumerable.Concat<byte>(other ?? Header, Payload ?? Media.Common.MemorySegment.Empty);
        }
        /// Generates a sequence of bytes containing the RtpHeader with the provided parameters and any data contained in the Payload.
        /// The sequence generated includes the SourceList and RtpExtension if present.
        /// </summary>
        /// <param name="payloadType">The optional payloadType to use</param>
        /// <param name="ssrc">The optional identifier to use</param>
        /// <param name="timestamp">The optional Timestamp to use</param>
        /// <returns>The binary seqeuence created.</returns>
        /// <remarks>
        /// To create the sequence a new RtpHeader is generated and eventually disposed.
        /// </remarks>
        public IEnumerable<byte> Prepare(int? payloadType, int? ssrc, int? sequenceNumber = null, int? timestamp = null, bool? marker = null) //includeHeader, includePayload, includePadding
        {
            try
            {
                //when all are null the header is the same... could use own header...
            }
            catch
            {
                throw;
            }
        }
        /// Provides a sample implementation of what would be required to complete a RtpPacket that has the IsComplete property False.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public virtual int CompleteFrom(System.Net.Sockets.Socket socket, Common.MemorySegment buffer)
        {
            if (IsReadOnly) throw new InvalidOperationException("Cannot modify a RtpPacket when IsReadOnly is false.");
            if (IsDisposed || IsComplete) return 0;
            int payloadCount = Payload.Count,
                octetsRemaining = payloadCount, //Cache how many octets remain in the payload
                offset = Payload.Offset,//Cache the offset in parsing 
                sourceListOctets = ContributingSourceListOctets,//Cache the amount of octets required in the ContributingSourceList.
                extensionSize = Header.Extension ? RtpExtension.MinimumSize : 0, //Cache the amount of octets required to read the ExtensionHeader
                recieved = 0;
            if (payloadCount < sourceListOctets)
            {
                //Calulcate the amount of octets to receive, ABS is weird and not required since paycount is checked to be less
                octetsRemaining = sourceListOctets - payloadCount; //Binary.Abs(payloadCount - sourceListOctets);
                if (m_OwnedOctets == null) m_OwnedOctets = new byte[octetsRemaining];
                else m_OwnedOctets = m_OwnedOctets.Concat(new byte[octetsRemaining]).ToArray();
                while (octetsRemaining > 0)
                {
                    //Receive octetsRemaining or less
                    int justReceived = Media.Common.Extensions.Socket.SocketExtensions.AlignedReceive(m_OwnedOctets, offset, octetsRemaining, socket, out error);
                    offset += justReceived;
                    octetsRemaining -= justReceived;
                }
            }
            offset = sourceListOctets;
            if (Header.Extension)
            {
                //Determine if the extension header was read
                octetsRemaining = RtpExtension.MinimumSize - (payloadCount - offset);
                if (octetsRemaining > 0) 
                {
                    //Allocte the memory for the extension header
                    if (m_OwnedOctets == null) m_OwnedOctets = new byte[octetsRemaining];
                    else m_OwnedOctets = m_OwnedOctets.Concat(new byte[octetsRemaining]).ToArray();
                    while (octetsRemaining > 0)
                    {
                        //Receive octetsRemaining or less
                        int justReceived = Media.Common.Extensions.Socket.SocketExtensions.AlignedReceive(m_OwnedOctets, offset, octetsRemaining, socket, out error);
                        offset += justReceived;
                        octetsRemaining -= justReceived;
                    }
                }
                using (RtpExtension extension = GetExtension())
                {
                    if (extension != null && false == extension.IsComplete)
                    {
                        //Cache the size of the RtpExtension (not including the Flags and LengthInWords [The Extension Header])
                        extensionSize = extension.Size - RtpExtension.MinimumSize;
                        //Calulcate the amount of octets to receive
                        octetsRemaining = (payloadCount - offset) - RtpExtension.MinimumSize;
                        {
                            //Allocte the memory for the required data
                            if (m_OwnedOctets == null) m_OwnedOctets = new byte[octetsRemaining];
                            else m_OwnedOctets = m_OwnedOctets.Concat(new byte[octetsRemaining]).ToArray();
                            while (octetsRemaining > 0)
                            {
                                //Receive octetsRemaining or less
                                int justReceived = Media.Common.Extensions.Socket.SocketExtensions.AlignedReceive(m_OwnedOctets, offset, octetsRemaining, socket, out error);
                                offset += justReceived;
                                octetsRemaining -= justReceived;
                            }
                        }
                    }
                }
            }
            if (Header.Padding)
            {
                //Double check this math
                octetsRemaining = PaddingOctets - payloadCount;
                {
                    //Allocte the memory for the required data
                    if (m_OwnedOctets == null) m_OwnedOctets = new byte[octetsRemaining];
                    else m_OwnedOctets = m_OwnedOctets.Concat(new byte[octetsRemaining]).ToArray();
                    while (octetsRemaining > 0)
                    {
                        System.Net.Sockets.SocketError error;
                        //Receive octetsRemaining or less
                        int justReceived = Media.Common.Extensions.Socket.SocketExtensions.AlignedReceive(m_OwnedOctets, offset, octetsRemaining, socket, out error);
                        offset += justReceived;
                    }
                }
            }
            Payload = new Common.MemorySegment(m_OwnedOctets, Payload.Offset, m_OwnedOctets.Length);
        }      
        /// Disposes of any private data this instance utilized.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && false == ShouldDispose) return;
            if (m_OwnsHeader && false == Common.IDisposedExtensions.IsNullOrDisposed(Header))
            {
                //Dispose it
                Header.Dispose();
            }
            {
                //Payload goes away when Disposing
                Payload.Dispose();
            }
            m_OwnedOctets = null;
        }
        {
            if (System.Object.ReferenceEquals(this, obj)) return true;
                 &&
                 other.Payload == Payload //SequenceEqual...
                 &&
                 other.GetHashCode() == GetHashCode();
        }
        public override int GetHashCode() { return Created.GetHashCode() ^ Header.GetHashCode(); }        
        /// Copies all of the data in the packet to the given destination. The amount of bytes copied is given by <see cref="Length"/>
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="offset"></param>
        public void CopyTo(byte[] destination, int offset)
        {
            offset += Header.CopyTo(destination, offset);
        }
        /// Calls <see cref="Update"/> on the <see cref="Payload"/> and <see cref="Synchronize"/> on the <see cref="Header"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        internal protected void Synchronize()
        {
            //Should check IsContiguous
            Payload.Update(ref m_OwnedOctets);
        }
        /// Indicates if the <see cref="Header"/> and <see cref="Payload"/> belong to the same array.
        /// </summary>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool IsContiguous()
        {
            return Header.IsContiguous() && Header.SegmentToLast10Bytes.Array == Payload.Array && Header.SegmentToLast10Bytes.Offset + Header.SegmentToLast10Bytes.Count == Payload.Offset;
        }
        public static bool operator ==(RtpPacket a, RtpPacket b)
        {
            object boxA = a, boxB = b;
            return boxA == null ? boxB == null : a.Equals(b);
        }
        public static bool operator !=(RtpPacket a, RtpPacket b) { return false == (a == b); }
        object ICloneable.Clone()
        {
            return this.Clone(true, true, true, true, false);
        }
        public bool TryGetBuffers(out System.Collections.Generic.IList<System.ArraySegment<byte>> buffer)
        {
            if (IsDisposed)
            {
                buffer = default(System.Collections.Generic.IList<System.ArraySegment<byte>>);
            }
            {
                Common.MemorySegmentExtensions.ToByteArraySegment(Header.First16Bits.m_Memory),
                Common.MemorySegmentExtensions.ToByteArraySegment(Header.SegmentToLast10Bytes),
                Common.MemorySegmentExtensions.ToByteArraySegment(Payload),
            };
        }

    }

    #endregion
}

namespace Media.UnitTests
{
    /// <summary>
    /// Provides tests which ensure the logic of the RtpPacket class is correct
    /// </summary>
    internal class RtpPacketUnitTests
    {
        public static void TestAConstructor_And_Reserialization()
        {
            //Cache a bitValue
            bool bitValue = false;
            for (int ibitValue = 0; ibitValue < 2; ++ibitValue)
            {
                //Make a bitValue after the 0th iteration
                if (ibitValue > 0) bitValue = Convert.ToBoolean(bitValue);
                for (int VersionCounter = 0; VersionCounter <= Media.Common.Binary.TwoBitMaxValue; ++VersionCounter)
                {
                    //Permute every possible value in the 7 bit PayloadCounter
                    for (int PayloadCounter = 0; PayloadCounter <= sbyte.MaxValue; ++PayloadCounter)
                    {
                        //Permute every possible value in the 4 bit ContributingSourceCounter
                        for (byte ContributingSourceCounter = byte.MinValue; ContributingSourceCounter <= Media.Common.Binary.FourBitMaxValue; ++ContributingSourceCounter)
                        {
                            int RandomId = Utility.Random.Next(), RandomSequenceNumber = Utility.Random.Next(ushort.MinValue, ushort.MaxValue), RandomTimestamp = Utility.Random.Next();
                            using (Media.Rtp.RtpPacket p = new Rtp.RtpPacket(VersionCounter, 
                                bitValue, !bitValue, bitValue, 
                                PayloadCounter, 
                                ContributingSourceCounter, 
                                RandomId, 
                                RandomSequenceNumber,
                                RandomTimestamp))
                            {
                                //Check the Version
                                System.Diagnostics.Debug.Assert(p.Version == VersionCounter, "Unexpected Version");
                                System.Diagnostics.Debug.Assert(p.Padding == bitValue, "Unexpected Padding");
                                System.Diagnostics.Debug.Assert(p.Extension == !bitValue, "Unexpected Extension");
                                System.Diagnostics.Debug.Assert(p.PayloadType == PayloadCounter, "Unexpected PayloadType");
                                System.Diagnostics.Debug.Assert(p.ContributingSourceCount == ContributingSourceCounter, "Unexpected ContributingSourceCounter");
                                System.Diagnostics.Debug.Assert(p.Length == Media.Rtp.RtpHeader.Length, "Unexpected Length");
                                using (Media.Rtp.RtpPacket s = new Rtp.RtpPacket(p.Prepare().ToArray(), 0))
                                {
                                    if (false == s.Prepare().SequenceEqual(p.Prepare())) throw new Exception("Unexpected Data");
                                    System.Diagnostics.Debug.Assert(s.Header.GetHashCode() == p.Header.GetHashCode(), "Unexpected GetHashCode");
                                    //This may or may not be desireable depding on what one is trying to do with the HashCode.
                                    //E.g. if your string both types of packet in a single collection using the the HasCode,
                                    //it's possible a collision will occur for some RtpPackets and RtcpPackets with the current GetHashCode implementation.
                                    //If two packets were created with the same DateTime then their HashCode will be equal.
                                    //System.Diagnostics.Debug.Assert(s.GetHashCode() == p.GetHashCode(), "Unexpected GetHashCode");
                                    //the created time is different
                                    //The values in the header and data are equal but not the same reference
                                    //Maybe should have a DataEqual overload... etc.
                            }
                        }
                    }
                }
            }
        }
    }
}