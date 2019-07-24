using SimpleJSON;

namespace ROSBridgeLib
{
    namespace sensor_msgs
    {
        public class NavSatFixMsg : ROSBridgeMsg
        {
            ROSBridgeLib.std_msgs.HeaderMsg _header;

            public enum NavSatStatus
            {
                NO_FIX = -1,        // unable to fix position
                FIX = 0,            // unaugmented fix
                SBAS_FIX = 1,        // with satellite-based augmentation
                GBAS_FIX = 2         // with ground-based augmentation
            }
            NavSatStatus _status;

            public enum NavSatService
            {
                GPS = 1,
                GLONASS = 2,
                COMPASS = 4,      // includes BeiDou.
                GALILEO = 8
            }
            NavSatService _service;

            double _latitude;
            double _longitude;
            double _altitude;
            double[] _position_covariance;

            public enum PositionCovarianceType
            {
                UNKNOWN = 0,
                APPROXIMATED = 1,
                DIAGONAL_KNOWN = 2,
                KNOWN = 3
            }
            PositionCovarianceType _position_covariance_type;


            public NavSatFixMsg(JSONNode msg)
            {
                _header = new std_msgs.HeaderMsg(msg["header"]);
                _status = (NavSatStatus)msg["status"]["status"].AsInt;
                _service = (NavSatService)msg["status"]["service"].AsInt;
                _latitude = msg["latitude"].AsFloat;
                _longitude = msg["longitude"].AsFloat;
                _altitude = msg["altitude"].AsFloat;

                JSONArray temp_covar_array = msg["position_covariance"].AsArray;
                _position_covariance = new double[temp_covar_array.Count];
                for (int i = 0; i < _position_covariance.Length; i++)
                {
                    _position_covariance[i] = temp_covar_array[i].AsDouble;
                }

                _position_covariance_type = (PositionCovarianceType)msg["position_covariance_type"].AsInt;
            }

            public static string getMessageType()
            {
                return "sensor_msgs/NavSatFix";
            }

            public std_msgs.HeaderMsg GetHeader()
            {
                return _header;
            }

            public NavSatStatus GetStatus()
            {
                return _status;
            }

            public NavSatService GetService()
            {
                return _service;
            }

            public double GetLatitude()
            {
                return _latitude;
            }

            public double GetLongitude()
            {
                return _longitude;
            }

            public double GetAltitude()
            {
                return _altitude;
            }

            public double[] GetPositionCovaraince()
            {
                return (double[])_position_covariance.Clone();
            }

            public PositionCovarianceType GetPositionCovarianceType()
            {
                return _position_covariance_type;
            }

            public override string ToString()
            {
                return string.Format("Latitude: {0}, Longitude: {1}, Altitude: {2}", _latitude, _longitude, _altitude);
            }
        }
    }
}
