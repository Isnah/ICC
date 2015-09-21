using System;

using UnityEngine;
using KSP;

namespace ICC
{
    // Currently unused
	public enum Datatype {
		I, D
	}

    // Currently unused
	public enum Mode {
		Orbit, Surface, Target
	}
	
    // Payload types. Remember to edit MAX_PAYLOAD_AMOUNT_PER PACKET when adding new payload types to this enum.
	public enum PayloadType : int {
		Altitude = 0, Targ_dist = 1, Surf_spd = 2, Targ_spd = 3, Orb_spd = 4, Apoapsis = 5, Apoapsis_r = 6, Periapsis = 7, Periapsis_r = 8, T_t_apo= 9, T_t_peri = 10, Stage = 11, Invalid = 12
	}

    public class ICCDefines
    {
        public static readonly int MAX_PAYLOAD_AMOUNT_PER_PACKET = 12; // All valid payloads. REMEMBER TO EDIT WHEN ADDING NEW PAYLOAD TYPES
    }

	public class ICCHelpers
	{
		public static int getsize (PayloadType type)
		{
			switch (type) {
			case PayloadType.Stage:
				return sizeof(int);
			default:
				return sizeof(double);
			}
		}

		public static String payloadHeader (PayloadType type)
		{
			switch (type) {
			case PayloadType.Altitude:
				return "ALTXXX";
			case PayloadType.Apoapsis:
				return "APOAPS";
            case PayloadType.Apoapsis_r:
                return "APOAPR";
			case PayloadType.Orb_spd:
				return "ORBSPD";
			case PayloadType.Periapsis:
				return "PERIAP";
            case PayloadType.Periapsis_r:
                return "PERIAR";
			case PayloadType.Stage:
				return "STAGEX";
			case PayloadType.Surf_spd:
				return "SRFSPD";
			case PayloadType.T_t_apo:
				return "TTAPOX";
			case PayloadType.T_t_peri:
				return "TTPERI";
			case PayloadType.Targ_dist:
				return "TRGDST";
			case PayloadType.Targ_spd:
				return "TRGSPD";
			default:
				return "INVALX";
			}
		}

		public static PayloadType decodeHeader (String header)
		{
			if ("ALTXXX".Equals (header)) {
				return PayloadType.Altitude;
			} else if ("APOAPS".Equals (header)) {
				return PayloadType.Apoapsis;
			} else if ("ORBSPD".Equals (header)) {
				return PayloadType.Orb_spd;
			} else if ("PERIAP".Equals (header)) {
				return PayloadType.Periapsis;
			} else if ("STAGEX".Equals (header)) {
				return PayloadType.Stage;
			} else if ("SRFSPD".Equals (header)) {
				return PayloadType.Surf_spd;
			} else if ("TTAPOX".Equals (header)) {
				return PayloadType.T_t_apo;
			} else if ("TTPERI".Equals (header)) {
				return PayloadType.T_t_peri;
			} else if ("TRGDST".Equals (header)) {
				return PayloadType.Targ_dist;
			} else if ("TRGSPD".Equals (header)) {
				return PayloadType.Targ_spd;
            } else if ("APOAPR".Equals (header)) {
                return PayloadType.Apoapsis_r;
            } else if ("PERIAR".Equals (header)) {
                return PayloadType.Periapsis_r;
			} else {
				return PayloadType.Invalid;
			}
		}

		// Will always return 8 bytes.
		public static Byte[] createChecksum (Byte[] packet)
		{
            Byte[] chksum = MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(UInt64.MinValue);


			for (uint i = 1; i < packet.Length / 8; ++i) {
				Byte[] part = new Byte[8];
				for(uint j = 0; j < 8; ++j) {
					part[j] = packet[i*8 + j];
				}

				for(uint j = 0; j < 8; ++j) {
					chksum[j] ^= part[j];
				}
			}

			return chksum;
		}

		public static String packetToString (Byte[] packet)
		{
			int index = 0;
			String str = "";

			// Get the id
			str += "\nID: " + BitConverter.ToString (packet, index, sizeof(UInt32)) + "\n";
			index += sizeof(UInt32);

            // Get the amount of payloads
            str += "Payload amount: " + MiscUtil.Conversion.EndianBitConverter.Big.ToUInt32(packet, index) + "\n";
            index += sizeof(UInt32);

			// [header size][smallest payload size][checksum size]
			while (index <= packet.Length - (3*sizeof(char) + sizeof(Int32) + sizeof(UInt64))) {
				String header = System.Text.Encoding.ASCII.GetString(packet, index, 3*sizeof(char)); //see below
				str += header + ": ";
				PayloadType type = decodeHeader(header);

				index += 3*sizeof(char); // As found in ICCOutgoing 6*sizeof(char) is twice as much as needed for 6 chars in ascii

				switch(type) {
				case PayloadType.Stage:
                        str += (MiscUtil.Conversion.EndianBitConverter.Big.ToInt32(packet, index) + "\n");
					index += sizeof(Int32);
					break;
				default:
                    str += (MiscUtil.Conversion.EndianBitConverter.Big.ToDouble(packet, index).ToString("F1") + "\n");
					index += sizeof(double);
					break;
				}
			}

			return str;
		}
	}
}

