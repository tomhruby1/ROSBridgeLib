using System.Collections;
using System.Text;
using SimpleJSON;
using ROSBridgeLib.std_msgs;

namespace ROSBridgeLib {
    namespace geometry_msgs {
        public class TfMessageMsg : ROSBridgeMsg {
            public TransformStampedMsg[] _transforms;
         
            public TfMessageMsg(JSONNode msg) {
                _transforms = new TransformStampedMsg[msg["transforms"].Count];
                for (int i = 0; i < _transforms.Length; i++)
                {
                    _transforms[i] = new TransformStampedMsg(msg["transforms"][i]);
                }
            }
 			
            public static string GetMessageType() {
                return "tf/tfMessage";
            }
			
            public TransformStampedMsg[] GetTransforms() {
                return _transforms;
            }

            public override string ToString()
            {
                string s = "[ ";
                for(int i = 0; i < _transforms.Length; i++)
                {
                    s += _transforms[i].ToString();
                    if (i < _transforms.Length - 1)
                        s += ", ";
                }
                s += " ]";
                
                return "tfMessage [transforms=" + s + "]";
            }
			
            public override string ToYAMLString() {
                string s = "[ ";
                for(int i = 0; i < _transforms.Length; i++)
                {
                    s += _transforms[i].ToString();
                    if (i < _transforms.Length - 1)
                        s += ", ";
                }
                s += " ]";
                
                return "{\"transforms\" : " + s +"}";
            }
        }
    }
}