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
		Altitude = 0, Targ_dist = 1, Surf_spd = 2, Targ_spd = 3, Orb_spd = 4, Apoapsis = 5, Apoapsis_r = 6, Periapsis = 7, Periapsis_r = 8, T_t_apo= 9, T_t_peri = 10, Stage = 11, 
        SOI_Num = 12, Latitude = 13, Longitude = 14, Orbital_period = 15, Gee_force = 16, Terrain_altitude = 17, Invalid = 18
	}

    public class ICCDefines
    {
        public static readonly int MAX_PAYLOAD_AMOUNT_PER_PACKET = 18; // All valid payloads. REMEMBER TO EDIT WHEN ADDING NEW PAYLOAD TYPES
    }

	public class ICCHelpers
	{
		public static int getsize (PayloadType type)
		{
			switch (type) {
			case PayloadType.Stage:
				return sizeof(int);
            case PayloadType.SOI_Num:
                return sizeof(byte);
            case PayloadType.Latitude:
            case PayloadType.Longitude:
                return sizeof(float);
			default:
				return sizeof(double);
			}
		}

		public static String payloadHeader (PayloadType type)
		{
			switch (type) {
			case PayloadType.Altitude:
				return "ALT";
			case PayloadType.Apoapsis:
				return "APA";
            case PayloadType.Apoapsis_r:
                return "APR";
			case PayloadType.Orb_spd:
				return "OSP";
			case PayloadType.Periapsis:
				return "PEA";
            case PayloadType.Periapsis_r:
                return "PER";
			case PayloadType.Stage:
				return "STG";
			case PayloadType.Surf_spd:
				return "SSP";
			case PayloadType.T_t_apo:
				return "TTA";
			case PayloadType.T_t_peri:
				return "TTP";
			case PayloadType.Targ_dist:
				return "TDI";
			case PayloadType.Targ_spd:
				return "TSP";
            case PayloadType.SOI_Num:
                return "SOI";
            case PayloadType.Latitude:
                return "LAT";
            case PayloadType.Longitude:
                return "LON";
            case PayloadType.Orbital_period:
                return "OPE";
            case PayloadType.Gee_force:
                return "GFO";
            case PayloadType.Terrain_altitude:
                return "AGL";
			default:
				return "INV";
			}
		}

		public static PayloadType decodeHeader (String header)
		{
			if ("ALT".Equals (header)) {
				return PayloadType.Altitude;
			} else if ("APA".Equals (header)) {
				return PayloadType.Apoapsis;
			} else if ("OSP".Equals (header)) {
				return PayloadType.Orb_spd;
			} else if ("PEA".Equals (header)) {
				return PayloadType.Periapsis;
			} else if ("STG".Equals (header)) {
				return PayloadType.Stage;
			} else if ("SSP".Equals (header)) {
				return PayloadType.Surf_spd;
			} else if ("TTA".Equals (header)) {
				return PayloadType.T_t_apo;
			} else if ("TTP".Equals (header)) {
				return PayloadType.T_t_peri;
			} else if ("TDS".Equals (header)) {
				return PayloadType.Targ_dist;
			} else if ("TSP".Equals (header)) {
				return PayloadType.Targ_spd;
            } else if ("APR".Equals (header)) {
                return PayloadType.Apoapsis_r;
            } else if ("PER".Equals (header)) {
                return PayloadType.Periapsis_r;
            } else if ("SOI".Equals (header)) {
                return PayloadType.SOI_Num;
            } else if ("LAT".Equals (header)) {
                return PayloadType.Latitude;
            } else if ("LON".Equals (header)) {
                return PayloadType.Longitude;
            } else if ("OPE".Equals (header)) {
                return PayloadType.Orbital_period;
            } else if ("OPE".Equals (header)) {
                return PayloadType.Gee_force;
            } else if ("AGL".Equals (header)) {
                return PayloadType.Terrain_altitude;
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
			while (index <= packet.Length - (3 + sizeof(Int32) + sizeof(UInt64))) {
				String header = System.Text.Encoding.ASCII.GetString(packet, index, 3); //see below
				str += header + ": ";
				PayloadType type = decodeHeader(header);

                index += 3; // 3 bytes needed for ASCII headers

				switch(type) {
				case PayloadType.Stage:
                    str += (MiscUtil.Conversion.EndianBitConverter.Big.ToInt32(packet, index) + "\n");
					index += sizeof(Int32);
					break;
                case PayloadType.SOI_Num:
                    str += packet[index];
                    index += 1;
                    break;
                case PayloadType.Latitude:
                case PayloadType.Longitude:
                    str += (MiscUtil.Conversion.EndianBitConverter.Big.ToSingle(packet, index).ToString("F1") + "\n");
                    index += sizeof(float);
                    break;
				default:
                    str += (MiscUtil.Conversion.EndianBitConverter.Big.ToDouble(packet, index).ToString("F1") + "\n");
					index += sizeof(double);
					break;
				}
			}

			return str;
		}

        public static byte SOI_to_SOI_Number(String soi)
        {
            String soi_lc = soi.ToLower();

            switch (soi_lc)
            {
                case "sun":
                    return 100;
                case "moho":
                    return 110;
                case "eve":
                    return 120;
                case "gilly":
                    return 121;
                case "kerbin":
                    return 130;
                case "mun":
                    return 131;
                case "minmus":
                    return 132;
                case "duna":
                    return 140;
                case "ike":
                    return 141;
                case "dres":
                    return 150;
                case "jool":
                    return 160;
                case "laythe":
                    return 161;
                case "vall":
                    return 162;
                case "tylo":
                    return 163;
                case "bop":
                    return 164;
                case "pol":
                    return 165;
                case "eeloo":
                    return 170;
                default:
                    return 0;
            }
        }
	}
}

