import json
import sys
import zmq

from IpcPacket import IpcPacket
from UserActions import do_get2, handle_do_get1


# We define our actions to have a packet argument plus inData tuple of [string,string] 
# where the convention is the first string is a name and the second is a value.
# Each action returns a string status plus an name-value tuple called outData.
def handle_action(in_packet):
    # Example action handler, expand based on actual actions
    action_handlers = {
        "do_get1": handle_do_get1,
        "do_get2": do_get2,
        # Add more actions here. Consider adding a "Close" or similar action to gracefully end the connection.
    }
    handler = action_handlers.get(in_packet.action)

    if handler is None:
        out_packet = in_packet.clone()
        out_packet.sequence_number = in_packet.sequence_number + 1
        out_packet.action = "FAIL"
        out_packet.status = f"Unknown action: {in_packet.action}"
        out_packet.reply_string = None
        return out_packet

    try:
        return handler(in_packet)
    except Exception as error:
        out_packet = in_packet.clone()
        out_packet.sequence_number = in_packet.sequence_number + 1
        out_packet.action = "FAIL"
        out_packet.status = f"Handler failed: {error}"
        out_packet.reply_string = None
        return out_packet
    
# Simple logging utility
def logit(msg):
    print(msg)
    return

# Read the request packet, assuming the first 5 chars is the encoded size
def read_request(socket):
    return socket.recv_string()
 
# The server creates a pipe and then continually reads request packets
# upon error, the pipe is re-created    
def runServerZeroMQ(server_address):
    context = zmq.Context()
    socket = context.socket(zmq.REP)
    socket.linger = 0

    try:
        logit(f"Starting ZeroMQ server at {server_address}")
        socket.bind(server_address)
        logit("IPC server is bound and waiting for requests.")

        while True:
            raw_packet = read_request(socket)

            try:
                request_packet = IpcPacket.deserializeFromJsonString(raw_packet)

                if request_packet.action == "Close":
                    reply_packet = request_packet.clone()
                    reply_packet.sequence_number = (
                        request_packet.sequence_number + 1
                    )
                    reply_packet.action = "SUCCESS"
                    reply_packet.status = "Success"
                    reply_packet.reply_string = None

                    socket.send_string(
                        IpcPacket.serializeToJsonString(reply_packet)
                    )

                    logit("Close command received.")
                    break

                reply_packet = handle_action(request_packet)

            except Exception as error:
                reply_packet = IpcPacket(
                    action="FAIL",
                    status=f"Request failed: {error}",
                )

            reply_json = IpcPacket.serializeToJsonString(reply_packet)
            socket.send_string(reply_json)

    except KeyboardInterrupt:
        logit("Server stopped.")

    except zmq.ZMQError as error:
        logit(f"ZeroMQ server failed: {error}")

    finally:
        logit(f"Closing server at {server_address}")
        socket.close(linger=0)
        context.term()
        
# Get the pipe name from the arguments, otherwise prompt.
def get_server_address():
    default_address = "tcp://127.0.0.1:5555"
    if len(sys.argv) > 1:
        return sys.argv[1]
    else:
        return input(f"Enter the server address [default: '{default_address}']: ") or default_address

if __name__ == "__main__":
#    server_address = r"tcp://127.0.0.1:5555"  # Example server_address, pass as argument or modify as needed


    #serverAddress = get_server_address()
    fullServerAddress = get_server_address()

    print(f"ServerZeroMQ using server full address={fullServerAddress}")   
    
    runServerZeroMQ(fullServerAddress)
