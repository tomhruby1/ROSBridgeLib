using System.Collections;
using System.Text;
using SimpleJSON;
using ROSBridgeLib.std_msgs;

namespace ROSBridgeLib {
    namespace geometry_msgs {
        public class TransformStampedMsg : ROSBridgeMsg {
            public HeaderMsg _header;
            public string _childFrameId;
            public TransformMsg _transform;
			
            public TransformStampedMsg(JSONNode msg) {
                _header = new HeaderMsg(msg["header"]);
                _childFrameId = msg["child_frame_id"];
                _transform = new TransformMsg(msg["transform"]);
            }
 			
            public static string GetMessageType() {
                return "geometry_msgs/TransformStamped";
            }
			
            public HeaderMsg GetHeader() {
                return _header;
            }

            public string GetChildFrameId()
            {
                return _childFrameId;
            }
            public TransformMsg GetTransform() {
                return _transform;
            }
			
            public override string ToString() {
                return "TransformStamped [header=" + _header.ToString() + ", child_frame_id=" +_childFrameId+", transform=" + _transform.ToString() + "]";
            }
			
            public override string ToYAMLString() {
                return "{\"header\" : " + _header.ToYAMLString() +", \"child_frame_id\" : "+_childFrameId +", \"transform\" : " + _transform.ToYAMLString() + "}";
            }
        }
    }
}