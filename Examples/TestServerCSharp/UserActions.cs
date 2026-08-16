using IpcNetMq;
using IpcNetMq.IpcNetMqHelpers;
using System.Text.Json;

namespace IpcTestServer
{
    public static class UserActions
    {

        /// <summary>
        /// The IpcPacket contains name-value pairs from the Request and for the Reply.
        /// The Names in the Request are Expression names. The Values are always string,so
        /// the decoding/encoding type is only implicitly known between the client/server.
        /// Most methods get the expressions and put results back in to the States.
        /// In this example method the number of pairs in both Expressions (Request) and States (Reply)
        /// are the same, but it's not a requirement.
        /// </summary>
        /// <param name="inPacket"/>
        /// <returns/>
        public static IpcPacket do_get1(IpcPacket inPacket)
        {
            // Implement your action logic here

            // Deserialize pairs holding request 'input' data and reply template.
            List<NameValuePair> requestList = JsonHelpers.DeserializePairListFromJsonString<NameValuePair>(inPacket.RequestString);
            List<NameValuePair> replyList = JsonHelpers.DeserializePairListFromJsonString<NameValuePair>(inPacket.ReplyString);

            //... and create a list for the 'output' Reply values.
            List<NameValuePair> outReplyList = new List<NameValuePair>();

            int index = 0;
            foreach (NameValuePair inPair in requestList)
            {
                NameValuePair outPair;

                // Use the reply list to get default values, otherwise
                // use the requestList
                if (index > 0 && (index < replyList.Count))
                {
                    outPair = replyList[index];
                }
                else
                    outPair = new NameValuePair(inPair);

                if (!double.TryParse(inPair.Value, out double dd))
                {
                    outPair.Value = "??"; // i.e. 'unknown'
                }
                else
                {
                    switch (inPair.Name)
                    {
                        case "exprOne":
                            outPair.Value = Math.Pow(dd, 1).ToString("0.00");
                            break;
                        case "exprTwo":
                            outPair.Value = Math.Pow(dd, 2).ToString("0.00");
                            break;
                        case "exprThree":
                            outPair.Value = Math.Pow(dd, 3).ToString("0.00");
                            break;
                        case "exprFour":
                            outPair.Value = Math.Pow(dd, 4).ToString("0.00");
                            break;
                        default:
                            outPair.Value = "-999.99";
                            break;
                    }
                }
                outReplyList.Add(outPair);

                index++;
            }

            var outPacket = new IpcPacket
            {
                //SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                ReplyString = JsonHelpers.SerializePairListToJsonString(outReplyList)
            };

            outPacket.Status = "Success";
            outPacket.RequestString = inPacket.RequestString;
            outPacket.WorldTime = DateTime.UtcNow.ToString();

            return outPacket;
        }

        public static IpcPacket do_get2(IpcPacket inPacket)
        {
            // Implement your action logic here
            List<NameValuePair> outPairList = new List<NameValuePair>
            {
                new NameValuePair { Name = "reply", Value = "result from do_get2" }
            };

            var outPacket = new IpcPacket
            {
                //SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                RequestString = JsonHelpers.SerializePairListToJsonString(outPairList)
            };
            return outPacket;
        }

        public static IpcPacket HandleAction(IpcPacket inPacket)
        {
            switch (inPacket.Action)
            {
                case "do_get1":
                    return UserActions.do_get1(inPacket);
                case "do_get2":
                    return UserActions.do_get2(inPacket);
                case "TestEnter":
                    return UserActions.TestEnter(inPacket);
                case "TestExit":
                    return UserActions.TestExit(inPacket);

                default:
                    var outData = $"Unknown action={inPacket.Action}";
                    return new IpcPacket
                    {
                        //SequenceNumber = inPacket.SequenceNumber,
                        Action = "FAIL",
                        RequestString = JsonSerializer.Serialize(outData)
                    };
            }
        }

        public static IpcPacket TestEnter(IpcPacket inPacket)
        {
            // Implement your action logic here
            List<NameValuePair> outPairList = new List<NameValuePair>
            {
                new NameValuePair { Name = "reply", Value = "result from TestEnter" }
            };
            var outPacket = new IpcPacket
            {
                //SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                RequestString = JsonHelpers.SerializePairListToJsonString(outPairList)
            };
            return outPacket;
        }

        public static IpcPacket TestExit(IpcPacket inPacket)
        {
            // Implement your action logic here
            List<NameValuePair> outPairList = new List<NameValuePair>
            {
                new NameValuePair { Name = "reply", Value = "result from TestExit" }
            };

            var outPacket = new IpcPacket
            {
                //SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                RequestString = JsonHelpers.SerializePairListToJsonString(outPairList)
            };
            return outPacket;
        }

        /// <summary>
        /// A template for your Action
        /// </summary>
        /// <param name="inPacket"/>
        /// <returns/>
        public static IpcPacket ActionTemplate(IpcPacket inPacket)
        {
            // Implement your action logic here
            List<NameValuePair> outPairList = new List<NameValuePair>
            {
                new NameValuePair { Name = "reply", Value = "result from do_get1" }
            };

            var outPacket = new IpcPacket
            {
                //SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                RequestString = JsonHelpers.SerializePairListToJsonString(outPairList)
            };
            return outPacket;
        }


    }
}
