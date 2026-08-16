using Xunit;
using IpcNetMq.IpcNetMqHelpers; // Your namespace
using System.Collections.Generic;

namespace IpcNetMq.Tests
{
    public class JsonHelpersTests
    {
        /// <summary>
        /// Note that the serialize/deserialize tests verify that these actions are done
        /// according to the attributes in the IpcPacket.
        /// </summary>
        [Fact]
        public void SerializeToJsonString_ValidPacket_ReturnsValidJson()
        {
            // Arrange
            var packet = new IpcPacket
            {
                //SequenceNumber = 1,
                Action = "TestAction",
                RequestString = "RequestData",
                ReplyString = "ReplyData"
            };

            // Act
            var jsonString = JsonHelpers.SerializeToJsonString(packet);

            // Assert
            Assert.Contains("\"sequence_number\":1", jsonString);
            Assert.Contains("\"action\":\"TestAction\"", jsonString);
        }

        [Fact]
        public void DeserializeFromJsonString_ValidJson_ReturnsValidPacket()
        {
            // Arrange
            string json = "{\"version\":\"V241007\", \"client_id\":\"abc123\", \"sequence_number\":1, \"utc_time\":\"2024-01-01T00:00:00Z\", \"action\":\"TestAction\", \"status\":\"\", \"options_string\":\"\", \"user_string\":\"\", \"request_string\":\"RequestData\", \"reply_string\":\"ReplyData\"}";
            //string json = "{\"SequenceNumber\":1,\"Action\":\"TestAction\",\"RequestString\":\"RequestData\",\"ReplyString\":\"ReplyData\"}";

            // Act
            var packet = JsonHelpers.DeserializeFromJsonString(json);

            // Assert
            Assert.Equal("V241007", packet.Version);
            Assert.Equal("abc123", packet.ClientId);
            Assert.Equal(1, packet.SequenceNumber);
            Assert.Equal("2024-01-01T00:00:00Z", packet.WorldTime);
            Assert.Equal("TestAction", packet.Action);
            Assert.Equal("RequestData", packet.RequestString);
            Assert.Equal("ReplyData", packet.ReplyString);
        }

        [Fact]
        public void BuildNameValuePairs_ValidPairs_ReturnsJsonString()
        {
            // Act
            string jsonString = JsonHelpers.BuildNameValuePairs(("name", "Fred"), ("age", "23"));

            // Assert
            Assert.Contains("\"name\":\"Fred\"", jsonString);
            Assert.Contains("\"age\":\"23\"", jsonString);
        }

        [Fact]
        public void DeserializePairListFromJsonString_ValidSingleObject_ReturnsList()
        {
            // Arrange
            string json = "{\"name\":\"example\",\"value\":\"test\"}";

            // Act
            var result = JsonHelpers.DeserializePairListFromJsonString<NameValuePair>(json);

            // Assert
            Assert.Single(result);
            Assert.Equal("example", result[0].Name);
            Assert.Equal("test", result[0].Value);
        }

        [Fact]
        public void DeserializePairListFromJsonString_ValidArray_ReturnsList()
        {
            // Arrange
            string json = "[{\"name\":\"example\",\"value\":\"test\"}, {\"name\":\"example2\",\"value\":\"test2\"}]";

            // Act
            var result = JsonHelpers.DeserializePairListFromJsonString<NameValuePair>(json);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("example", result[0].Name);
            Assert.Equal("test", result[0].Value);
        }
    }
}
