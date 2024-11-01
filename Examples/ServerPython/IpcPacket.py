import json
import datetime

class IpcPacket:
    def __init__(self, version="V241007",  client_id=None, sequence_number=0, world_time=None, action=None, status=None, options_string=None, context_string=None, request_string=None, response_string=None):
        self.version = version
        self.client_id = client_id
        self.sequence_number = sequence_number
        self.world_time = world_time or datetime.datetime.now().isoformat()
        self.action = action
        self.status = status
        self.options_string = options_string
        self.context_string = context_string
        self.request_string = request_string
        self.response_string = response_string

    def clone(self):
        return IpcPacket(self.version, self.client_id, self.sequence_number, self.world_time, 
                           self.action, self.options_string, self.context_string, self.request_string, self.response_string)

    def __str__(self):
        return f"Version=[{self.version}] #=[{self.sequence_number}] WorldTime={self.world_time} " 
        f" Action=[{self.action}] Options=[{self.options_string}] Context=[{self.context_string}] Request={self.request_string} Response={self.response_string}"

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

