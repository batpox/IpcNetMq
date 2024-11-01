using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using IpcNetMq;
using IpcNetMq.IpcNetMqHelpers;

namespace IpcTestServer
{
    public static class UserActions
    {

        /// <summary>
        /// The IpcPacket contains name-value pairs from the Request and for the Response.
        /// The Names in the Request are Expression names. The Values are always string,so
        /// the decoding/encoding type is only implicitly known between the client/server.
        /// Most methods get the expressions and put results back in to the States.
        /// In this example method the number of pairs in both Expressions (Request) and States (Response)
        /// are the same, but it's not a requirement.
        /// </summary>
        /// <param name="inPacket"></param>
        /// <returns></returns>
        public static IpcPacket do_get1(IpcPacket inPacket)
        {
            // Implement your action logic here

            // Deserialize pairs holding request 'input' data and response template.
            List<NameValuePair> requestList = JsonHelpers.DeserializePairListFromJsonString<NameValuePair>(inPacket.RequestString);
            List<NameValuePair> responseList = JsonHelpers.DeserializePairListFromJsonString<NameValuePair>(inPacket.ResponseString);

            //... and create a list for the 'output' Response values.
            List<NameValuePair> outResponseList = new List<NameValuePair>();

            int index = 0;
            foreach (NameValuePair inPair in requestList)
            {
                NameValuePair outPair;

                // Use the response list to get default values, otherwise
                // use the requestList
                if (index > 0 && (index < responseList.Count))
                {
                    outPair = responseList[index];
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
                outResponseList.Add(outPair);

                index++;
            }

            var outPacket = new IpcPacket
            {
                SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                ResponseString = JsonHelpers.SerializePairListToJsonString(outResponseList)
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
                new NameValuePair { Name = "response", Value = "result from do_get2" }
            };

            var outPacket = new IpcPacket
            {
                SequenceNumber = inPacket.SequenceNumber,
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
                        SequenceNumber = inPacket.SequenceNumber,
                        Action = "FAIL",
                        RequestString = JsonConvert.SerializeObject(outData)
                    };
            }
        }

        public static IpcPacket TestEnter(IpcPacket inPacket)
        {
            // Implement your action logic here
            List<NameValuePair> outPairList = new List<NameValuePair>
            {
                new NameValuePair { Name = "response", Value = "result from TestEnter" }
            };
            var outPacket = new IpcPacket
            {
                SequenceNumber = inPacket.SequenceNumber,
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
                new NameValuePair { Name = "response", Value = "result from TestExit" }
            };

            var outPacket = new IpcPacket
            {
                SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                RequestString = JsonHelpers.SerializePairListToJsonString(outPairList)
            };
            return outPacket;
        }

        /// <summary>
        /// A template for your Action
        /// </summary>
        /// <param name="inPacket"></param>
        /// <returns></returns>
        public static IpcPacket ActionTemplate(IpcPacket inPacket)
        {
            // Implement your action logic here
            List<NameValuePair> outPairList = new List<NameValuePair>
            {
                new NameValuePair { Name = "response", Value = "result from do_get1" }
            };

            var outPacket = new IpcPacket
            {
                SequenceNumber = inPacket.SequenceNumber,
                Action = "SUCCESS",
                RequestString = JsonHelpers.SerializePairListToJsonString(outPairList)
            };
            return outPacket;
        }


    }
}
