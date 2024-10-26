import json
import math
import sys
import zmq

#import win32pipe, win32file  # For NamedPipes
import pywintypes
from IpcPacket import IpcPacket, NameValuePair

from UserActions import do_get1, do_get2

# We define our actions to have a packet argument plus inData tuple of [string,string] 
# where the convention is the first string is a name and the second is a value.
# Each action returns a string status plus an name-value tuple called outData.
def handle_action(inPacket):
    # Example action handler, expand based on actual actions
    action_handlers = {
        "do_get1": do_get1,
        "do_get2": do_get2
        # Add more actions here. Consider adding a "Close" or similar action to gracefully end the connection.
    }
    action = inPacket.action
    
    if action in action_handlers:
        outPacket = action_handlers[action](inPacket)

    else:
        outData = f"Unknown action={action}"
        outPacket = IpcPacket(sequence_number=inPacket.sequence_number, action="FAIL", request_string=json.dumps(outData))

    return outPacket

# Simple logging utility
def logit(msg):
    print(msg)
    return

# Read the request packet, assuming the first 5 chars is the encoded size
def read_request(socket):
    # Wait for the next request from client
    rawPacket = socket.recv_string()
    ##print(f"Received request: {rawPacket}")
    
    packet = rawPacket
    
    return packet
 
# The server creates a pipe and then continually reads request packets
# upon error, the pipe is re-created    
def runServerZeroMQ(server_address):
    
    while True:
        try:
            context = zmq.Context()
    
            logit(f"Starting ZeroMQ server at {server_address}")

            try:
                # Get a new Connection
                keepConnecting = True
                while keepConnecting:
                    
                    logit(f"Waiting for client (at {server_address})...")
                    socket = context.socket(zmq.REP)
                    socket.bind(server_address)
                    logit(f"Client now bound to socket. Reading request...")

                    # Get a new request. On exception, close and leave loop so new connection is started
                    
                    keepReading = True
                    while keepReading:
                        try:
                            rawPacket = read_request(socket)
                            #packet_json = rawPacket.decode("utf-8")
                            
                            packet = IpcPacket.deserializeFromJsonString(rawPacket)
                            if packet.action == "Close":
                                logit("Close command received. Closing connection.")
                                keepConnecting = False
                                break  # Exit the loop to close the connection
                            
                            # Take the necessary actions
                            replyPacket = handle_action(packet)
                            if replyPacket:
                                replyjson = IpcPacket.serializeToJsonString(replyPacket)
                                socket.send_string(replyjson)
                                
                        except pywintypes.error as e:
                            logit(f"ReadFile failed. Reason={e}")
                            keepReading = False
                            break  # Exit the reading loop if an error occurred
                        
            except Exception as e:
                logit(f"Server failed. Reason={e}")
                keepReading = False
                socket.disconnect()
                
            finally:
                logit(f"Server Closing Handle: ")
                socket.disconnect()
                
        except Exception as e:
            logit(f"Server disconnect Error. Reason={e}")
            
        finally:
            logit(f"Server starting rebuild of ServerAddress={server_address}")    
            socket.disconnect()
            

# Get the pipe name from the arguments, otherwise prompt.
def get_server_address():
    default_address = "tcp://127.0.0.1:5555"
    if len(sys.argv) > 1:
        return sys.argv[1]
    else:
        return input(f"Enter the server address [default: '{default_address}']: ") or default_address

if __name__ == "__main__":
#    server_address = r"tcp://127.0.0.1:5555"  # Example server_address, pass as argument or modify as needed


    serverAddress = get_server_address()
    fullServerAddress = get_server_address()
    

    print(f"Using server address={serverAddress} (full address={fullServerAddress})")
    
    runServerZeroMQ(fullServerAddress)
