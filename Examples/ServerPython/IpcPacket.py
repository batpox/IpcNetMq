import json
import datetime

class IpcPacket:
    def __init__(self, version="V260107",  client_id=None, sequence_number=0, world_time=None, action=None, status=None, options_string=None, context_string=None, request_string=None, reply_string=None):
        self.version = version
        self.client_id = client_id
        self.sequence_number = sequence_number
        self.world_time = (
            world_time 
            or datetime.datetime.now(datetime.timezone.utc).isoformat()
        )
        self.action = action
        self.status = status
        self.options_string = options_string
        self.context_string = context_string
        self.request_string = request_string
        self.reply_string = reply_string

    def clone(self):
        return IpcPacket(
            version=self.version, 
            client_id=self.client_id, 
            sequence_number=self.sequence_number, 
            world_time=self.world_time, 
            action=self.action, 
            status=self.status, 
            options_string=self.options_string, 
            context_string=self.context_string, 
            request_string=self.request_string, 
            reply_string=self.reply_string
        )

    def __str__(self):
        return (
            f"Version=[{self.version}] "
            f"#=[{self.sequence_number}] "
            f"WorldTime={self.world_time} "
            f"Action=[{self.action}] "
            f"Status=[{self.status}] "
            f"Options=[{self.options_string}] "
            f"Context=[{self.context_string}] "
            f"Request={self.request_string} "
            f"Reply={self.reply_string}"
        )

    @staticmethod
    def serializeToJsonString(obj):
        return json.dumps(obj, default=lambda o: o.__dict__, sort_keys=True)

    @staticmethod
    def deserializeFromJsonString(json_string):
        data = json.loads(json_string)
        return IpcPacket(**data)

    def writeToFile(self, filepath):
        try:
            with open(filepath, 'w') as file:
                file.write(IpcPacket.serializeToJsonString(self))
            return True
        except Exception as e:
            print(f"Error writing to file: {e}")
            return False

# Static method to read from a file. Used for debugging
    @staticmethod
    def readFromFile(filepath):
        try:
            with open(filepath, 'r') as file:
                data = file.read()
            return IpcPacket.deserializeFromJsonString(data)
        except Exception as e:
            print(f"Error reading from file: {e}")
            return None

# Additional helper methods, if needed, can be added here.
class NameValuePair:
    def __init__(self, Name, Value):
        self.Name = Name
        self.Value = Value

