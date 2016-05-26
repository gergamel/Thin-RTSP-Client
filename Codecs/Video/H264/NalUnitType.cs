﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Media.Codecs.Video.H264
{
    public static class NalUnitType
    {
        //Mpeg start codes
        public static byte[] StartCodePrefix = new byte[] { 0x00, 0x00, 0x01 };
        public const byte SingleTimeAggregationA = 24;
        {
            {
                case Reserved:
                //nalType >= 16 && nalType <= 18
                case 16:
                case 17:
                case 18:
                //nalType >= 22 && nalType <= 23
                case 22:
                case 23:
                    return true;
                default: return false;
            }
        }
        public static bool IsReserved(byte nalType) { return IsReserved(ref nalType); }
        {
            switch (nalType)
            {
                case CodedSlice:
                case DataPartitionA:
                case InstantaneousDecoderRefresh:
                    return true;
                default: return false;
            }
        }
        public static bool IsSlice(byte nalType) { return IsSlice(ref nalType); }
    }
}