using System;

using OpenNETCF;
using MiscUtil;

using UnityEngine;
using KSP;


/* Packets will be on the form
 * 
 * [Packet ID (4 Bytes)]
 * [Amount of payloads (4 Bytes)]
 * [PayloadHeader(3 bytes)][Payload(Varies)]
 * ([PayloadHeader][Payload])
 * [Padding to make it divisible by 8]
 * [Checksum (8 Bytes)]
 * 
 * 
 * Payload headers:
 * Headers are 3 bytes of ascii letters, as follows:
 * 
 * Altitude        = ALT
 * Target distance = TDS // Not fully implemented
 * Surface Speed   = SSP
 * Target Speed    = TSP
 * Orbit Speed     = OSP
 * Apoapsis Alt    = APA
 * Apoapsis Rad    = APA
 * Periapsis Alt   = PEA
 * Periapsis Rad   = PER
 * Time to Apo     = TTA
 * Time to Peri    = TTP
 * Stage           = STG
 * SOI number      = SOI
 * Latitude        = LAT
 * Longitude       = LON
 * Orbital period  = OPE
 * G-Force         = GFO
 * Altitude Ground = AGL
 * 
 * Being Implemented
 * 
 * To be implemented:
 * 
 * Atmo density    = ATM
 * Liquid Fuel Max = LFM
 * LiFuel Current  = LFC
 * Oxidizer Nax    = OXM
 * Oxidizer Curr   = OXC
 * ElCharge Max    = ECM
 * ElCharge Curr   = ECC
 * Monoprop Max    = MPM
 * Monoprop Curr   = MPC
 * Intake Air Max  = IAM
 * Intake Air Curr = IAC
 * Solid Fuel Max  = SFM
 * Solid Fuel Curr = SFC
 * Xenon Gas Max   = XGM
 * Xenon Gas Curr  = XGC

 * 
 * Might be implemented:
 * 
 * Mission time    = MTX
 * 
 */

namespace ICC
{
    [KSPAddon(KSPAddon.Startup.Flight, true)]
	public class ICCOutgoing : MonoBehaviour
	{
		private class Attributes 
		{
			public static UInt32 packet_counter = 0;

            public static bool[] payload_switch_list = { true, false, true, false, true, true, false, true, false, false, false, true, true, true, true, true, true, false };
            /*
             * THE FOLLOWING COMMENTED OUT CODE ILLUSTRATES THE ABOVE ARRAY
             * THE VALUES ARE IN ORDER OF PayloadType IN ICCHelpers
			public static bool altitude  = true;
			public static bool targ_dist = false;
			public static bool surf_spd  = true;
			public static bool targ_spd  = false;
			public static bool orb_spd   = true;
			public static bool apoapsis  = true;
            public static bool apoaps_r  = false;
			public static bool periapsis = true;
            public static bool periap_r  = false;
			public static bool t_t_apo   = false;
			public static bool t_t_peri  = false;
			public static bool stage     = true;
            ------------------ soi       = true;
            ------------------ latitude  = true;
            ------------------ latitude  = true;
            ------------------ orb_per   = true;
            ------------------ gee_force = true;
            ------------------ terr_alt  = false;
            */

            public static string port_name = "COM4";    // CHANGE THESE
            public static int baud_rate    = 115200;    // AS NEEDED

            public static int update_rate      = 100;                       // Time between updates in milliseconds
            public static DateTime prev_time   = new DateTime(2015, 1, 1);  // Previous time a packet was sent, initially instantiated at 1/1/2010


            // Previous values so we know whether we'll need to send new values.
            public static double prev_altitude  = -1.0;
            public static double prev_targ_dist = -1.0;
            public static double prev_surf_spd  = -1.0;
            public static double prev_targ_spd  = -1.0;
            public static double prev_orb_spd   = -1.0;
            public static double prev_apoapsis  = -1.0;
            public static double prev_apoaps_r  = -1.0;
            public static double prev_periapsis = -1.0;
            public static double prev_periaps_r = -1.0;
            public static double prev_t_t_apo   = -1.0;
            public static double prev_t_t_peri  = -1.0;
            public static int    prev_stage     = -1;
            public static byte   prev_soi       =  0;
            public static float  prev_latitude  = -361.0f;
            public static float  prev_longitude = -361.0f;
            public static double prev_orb_per   = -1.0;
            public static double prev_gee_force = -1.0;
            // previous terrain altitude not needed

            // The difference needed for a new item to be considered different enough to resend.
            public static readonly double ALTITUDE_APPROX_EQUALITY = 0.5;
            public static readonly double SPEED_APPROX_EQUALITY    = 0.1;
            public static readonly double GAME_TIME_APPROX_EQUAL   = 1.0;
            public static readonly double DEGREES_APPROX_EQUALITY  = 0.1;
		}

        private OpenNETCF.IO.Ports.SerialPort port = null;

		void Start ()
		{
            DontDestroyOnLoad(this);

			print ("[ICC] Starting outgoing communications");

            if(port == null) port = new OpenNETCF.IO.Ports.SerialPort(Attributes.port_name, Attributes.baud_rate);
            port.Open();

            print("[ICC] Port Open");

            //port.WriteLine("[ICC] Port Open\n");
		}

		void Update ()
		{
            if (HighLogic.LoadedSceneIsFlight)
            {
                TimeSpan since_last_packet = System.DateTime.Now - Attributes.prev_time;
                if (since_last_packet.TotalMilliseconds > Attributes.update_rate)
                {
                    Attributes.prev_time = System.DateTime.Now;

                    PayloadType[] payloads = findActivePayloadTypes();

                    if (payloads.Length != 0)
                    {

                        Byte[] packet = CreatePacket(payloads);

                        updatePrevPacketAttr(payloads);

                        print("[ICC] Checksum check, should be 0: " + MiscUtil.Conversion.EndianBitConverter.Big.ToUInt64(ICCHelpers.createChecksum(packet), 0));
                        String str = ICCHelpers.packetToString(packet);
                        port.Write(packet, 0, packet.Length);

                        //port.WriteLine(str);

                        print("[ICC] Decoded package follows\n");
                        print(str + "\n");
                    }
                }
            }
		}


		void OnDestroy ()
		{
			print ("[ICC] Stopping outgoing communications");
            if (port.IsOpen) port.Close();
		}

		private Byte[] CreatePacket (PayloadType[] payloads)
		{
			uint index = 0;

			int size = 0;

			size += sizeof(UInt32); // Room for packet ID
            size += sizeof(UInt32); // Room for payload amount information

			for (uint i = 0; i < payloads.Length; ++i) {
				size += 3; // 3 bytes is required for the three letter ASCII headers
				size += ICCHelpers.getsize (payloads [i]);
			}

			// Make sure the packet is a multiple of 8
            int remnant = size % 8;
            if (remnant != 0) size += 8 - remnant;

			size += sizeof(UInt64); // Make room for the checksum. The numbers at the end are all 0, so it will not interfere with the XOR

			print ("[ICC] Size: " + size);

			Byte[] packet = new Byte[size];

            Byte[] id = MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(Attributes.packet_counter);
			Attributes.packet_counter++;

			for (uint i = 0; i < id.Length; ++i, ++index) {
				packet [index] = id [i];
			}

            id = MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(payloads.Length);

            for (uint i = 0; i < id.Length; ++i, ++index)
            {
                packet[index] = id[i];
            }

			for (uint i = 0; i < payloads.Length; ++i) {
				Byte[] header = System.Text.Encoding.ASCII.GetBytes (ICCHelpers.payloadHeader (payloads [i]));
				print ("[ICC] Created header: " + BitConverter.ToString (header));
				Byte[] payload = createPayload (payloads [i]);

				for (uint j = 0; j < header.Length; ++j, ++index) {
					packet [index] = header [j];
				}

				for (uint j = 0; j < payload.Length; ++j, ++index) {
					packet [index] = payload [j];
				}
			}

			// Time to add the checksum to the end
            // First make sure it will be at the end
            if (remnant != 0) index += 8 - Convert.ToUInt32(remnant);
			Byte[] chksum = ICCHelpers.createChecksum (packet);

			for(uint i = 0; i < sizeof(UInt64); ++i, ++index) {
				packet[index] = chksum[i];
			}

			print ("[ICC] Created Packet: " + BitConverter.ToString(packet));

			return packet;
		}

		private Byte[] createPayload (PayloadType type)
		{
			switch (type) {
			case PayloadType.Altitude:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.altitude);
			case PayloadType.Targ_dist:
                // TODO
				return new Byte[sizeof(double)];
			case PayloadType.Surf_spd:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.srfSpeed);
			case PayloadType.Targ_spd:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ship_tgtSpeed);
			case PayloadType.Orb_spd:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.obt_speed);
			case PayloadType.Apoapsis:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.orbit.ApA);
			case PayloadType.Periapsis:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.orbit.PeA);
			case PayloadType.T_t_apo:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.orbit.timeToAp);
			case PayloadType.T_t_peri:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.orbit.timeToPe);
			case PayloadType.Stage:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.currentStage);
            case PayloadType.SOI_Num:
                byte[] payload = new byte[1];
                payload[0] = ICCHelpers.SOI_to_SOI_Number(FlightGlobals.ActiveVessel.orbit.referenceBody.name);
                return payload;
            case PayloadType.Latitude:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes((float)FlightGlobals.ActiveVessel.latitude);
            case PayloadType.Longitude:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes((float)FlightGlobals.ActiveVessel.longitude);
            case PayloadType.Orbital_period:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.orbit.period);
            case PayloadType.Gee_force:
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(FlightGlobals.ActiveVessel.geeForce);
            case PayloadType.Terrain_altitude:
                double agl = FlightGlobals.ActiveVessel.altitude - FlightGlobals.ActiveVessel.terrainAltitude;
                if (agl > FlightGlobals.ActiveVessel.altitude) agl = FlightGlobals.ActiveVessel.altitude; // If the terrain is lower than sea level, set agl to altitude above sea level
                return MiscUtil.Conversion.EndianBitConverter.Big.GetBytes(agl);
			default:
				print("[ICC] ERROR. Unused PayloadType in createPayload()");
				return new Byte[sizeof(double)];
			}
		}

        private PayloadType[] findActivePayloadTypes()
        {
            int payload_amt = 0;

            // First check for activated attributes.
            for (int i = 0; i < Attributes.payload_switch_list.Length; ++i)
            {
                if (Attributes.payload_switch_list[i]) payload_amt++;
            }

            PayloadType[] temp_payload_array = new PayloadType[payload_amt];
            bool[] needs_to_be_sent = new bool[payload_amt];
            int curr_index = 0;

            payload_amt = 0;
            
            // Next get active attributes and check which of them actually needs to get sent
            for (int i = 0; i < Attributes.payload_switch_list.Length; ++i)
            {
                if (Attributes.payload_switch_list[i])
                {
                    if (!checkPayloadTypeAttributeApproxEq((PayloadType)i))
                    {
                        payload_amt++;
                        needs_to_be_sent[curr_index] = true;
                    }
                    else
                    { 
                        needs_to_be_sent[curr_index] = false;
                    }
                    temp_payload_array[curr_index] = (PayloadType)i;
                    curr_index++;
                }
            }

            PayloadType[] payload_array = new PayloadType[payload_amt];
            curr_index = 0;

            // Only add the attributes that need to be sent to the return array.
            for (int i = 0; i < temp_payload_array.Length; ++i)
            {
                if (needs_to_be_sent[i])
                {
                    payload_array[curr_index] = temp_payload_array[i];
                    curr_index++;
                }
            }

            return payload_array;
        }

        private bool checkPayloadTypeAttributeApproxEq(PayloadType type)
        {
            switch (type)
            {
                case PayloadType.Altitude:
                case PayloadType.Terrain_altitude:
                    return Math.Abs(Attributes.prev_altitude - FlightGlobals.ActiveVessel.altitude) < Attributes.ALTITUDE_APPROX_EQUALITY;
                case PayloadType.Apoapsis:
                    return Math.Abs(Attributes.prev_apoapsis - FlightGlobals.ActiveVessel.orbit.ApA) < Attributes.ALTITUDE_APPROX_EQUALITY;
                case PayloadType.Apoapsis_r:
                    return Math.Abs(Attributes.prev_apoaps_r - FlightGlobals.ActiveVessel.orbit.ApR) < Attributes.ALTITUDE_APPROX_EQUALITY;
                case PayloadType.Orb_spd:
                    return Math.Abs(Attributes.prev_orb_spd - FlightGlobals.ActiveVessel.obt_speed) < Attributes.SPEED_APPROX_EQUALITY;
                case PayloadType.Periapsis:
                    return Math.Abs(Attributes.prev_periapsis - FlightGlobals.ActiveVessel.orbit.PeA) < Attributes.ALTITUDE_APPROX_EQUALITY;
                case PayloadType.Periapsis_r:
                    return Math.Abs(Attributes.prev_periaps_r - FlightGlobals.ActiveVessel.orbit.PeR) < Attributes.ALTITUDE_APPROX_EQUALITY;
                case PayloadType.Stage:
                    return Attributes.prev_stage == FlightGlobals.ActiveVessel.currentStage;
                case PayloadType.Surf_spd:
                    return Math.Abs(Attributes.prev_surf_spd - FlightGlobals.ActiveVessel.srfSpeed) < Attributes.SPEED_APPROX_EQUALITY;
                case PayloadType.T_t_apo:
                    return Math.Abs(Attributes.prev_t_t_apo - FlightGlobals.ActiveVessel.orbit.timeToAp) < Attributes.GAME_TIME_APPROX_EQUAL;
                case PayloadType.T_t_peri:
                    return Math.Abs(Attributes.prev_t_t_peri - FlightGlobals.ActiveVessel.orbit.timeToPe) < Attributes.GAME_TIME_APPROX_EQUAL;
                case PayloadType.Targ_dist:
                    // TODO
                    return true;
                case PayloadType.Targ_spd:
                    return Math.Abs(Attributes.prev_targ_spd - FlightGlobals.ship_tgtSpeed) < Attributes.SPEED_APPROX_EQUALITY;
                case PayloadType.SOI_Num:
                    return Attributes.prev_soi == ICCHelpers.SOI_to_SOI_Number(FlightGlobals.ActiveVessel.orbit.referenceBody.name);
                case PayloadType.Latitude:
                    return Math.Abs(Attributes.prev_latitude - FlightGlobals.ActiveVessel.latitude) < Attributes.DEGREES_APPROX_EQUALITY;
                case PayloadType.Longitude:
                    return Math.Abs(Attributes.prev_longitude - FlightGlobals.ActiveVessel.longitude) < Attributes.DEGREES_APPROX_EQUALITY;
                case PayloadType.Orbital_period:
                    return Math.Abs(Attributes.prev_orb_per - FlightGlobals.ActiveVessel.orbit.period) < Attributes.GAME_TIME_APPROX_EQUAL;
                case PayloadType.Gee_force:
                    return Math.Abs(Attributes.prev_gee_force - FlightGlobals.ActiveVessel.geeForce) < Attributes.SPEED_APPROX_EQUALITY;
                default:
                    print("[ICC] ERROR. Trying to check attribute equality with invalid payload type");
                    return true;
            }
        }

        private void updatePrevPacketAttr(PayloadType[] payloads)
        {
            for (int i = 0; i < payloads.Length; ++i)
            {
                switch (payloads[i])
                {
                    case PayloadType.Altitude:
                    case PayloadType.Terrain_altitude:
                        Attributes.prev_altitude = FlightGlobals.ActiveVessel.altitude;
                        break;
                    case PayloadType.Apoapsis:
                        Attributes.prev_apoapsis = FlightGlobals.ActiveVessel.orbit.ApA;
                        break;
                    case PayloadType.Apoapsis_r:
                        Attributes.prev_apoaps_r = FlightGlobals.ActiveVessel.orbit.ApR;
                        break;
                    case PayloadType.Orb_spd:
                        Attributes.prev_orb_spd = FlightGlobals.ActiveVessel.obt_speed;
                        break;
                    case PayloadType.Periapsis:
                        Attributes.prev_periapsis = FlightGlobals.ActiveVessel.orbit.PeA;
                        break;
                    case PayloadType.Periapsis_r:
                        Attributes.prev_periaps_r = FlightGlobals.ActiveVessel.orbit.PeR;
                        break;
                    case PayloadType.Stage:
                        Attributes.prev_stage = FlightGlobals.ActiveVessel.currentStage;
                        break;
                    case PayloadType.Surf_spd:
                        Attributes.prev_surf_spd = FlightGlobals.ActiveVessel.srfSpeed;
                        break;
                    case PayloadType.T_t_apo:
                        Attributes.prev_t_t_apo = FlightGlobals.ActiveVessel.orbit.timeToAp;
                        break;
                    case PayloadType.T_t_peri:
                        Attributes.prev_t_t_peri = FlightGlobals.ActiveVessel.orbit.timeToPe;
                        break;
                    case PayloadType.Targ_dist:
                        // TODO
                        break;
                    case PayloadType.Targ_spd:
                        Attributes.prev_targ_spd = FlightGlobals.ship_tgtSpeed;
                        break;
                    case PayloadType.SOI_Num:
                        Attributes.prev_soi = ICCHelpers.SOI_to_SOI_Number(FlightGlobals.ActiveVessel.orbit.referenceBody.name);
                        break;
                    case PayloadType.Latitude:
                        Attributes.prev_latitude = (float)FlightGlobals.ActiveVessel.latitude;
                        break;
                    case PayloadType.Longitude:
                        Attributes.prev_longitude = (float)FlightGlobals.ActiveVessel.longitude;
                        break;
                    case PayloadType.Orbital_period:
                        Attributes.prev_orb_per = FlightGlobals.ActiveVessel.orbit.period;
                        break;
                    case PayloadType.Gee_force:
                        Attributes.prev_gee_force = FlightGlobals.ActiveVessel.geeForce;
                        break;
                    default:
                        print("[ICC] ERROR. Trying to update invalid payload type");
                        break;
                }
            }
        }
	}
}