import json
import math
import sys

import win32pipe, win32file, pywintypes
from IpcPacket import IpcPacket, NameValuePair


#========================================================================
def convert_to_number(value):
    try:
        # Try to convert the value to a float
        number = float(value)
        return number
    except ValueError:
        # If conversion fails, return None or handle the case as needed
        return None
    
#========================================================================
# Assume name,integer tuples and return squared integers
def do_get1(inPacket):
    
    ##logit("Handling do_get1")
    # Decode request as tuples of string,int
    contextData = json.loads(inPacket.context_string)
    requestData = json.loads(inPacket.request_string)    
    replyData = json.loads(inPacket.reply_string)
    
    status = "OK"
    
    # Get the expression key name and the value (which is an integer in this case)
    # Modify the value and then place the new value into the replyData.
    simTime = getValueByName(contextData, 'SimTime')
    
    for item in requestData:
        try:
            expr = item["name"]
            value = item["value"]
            numericalValue = convert_to_number(value)
            computedValue = 0
            if expr == 'exprOne':
                computedValue = numericalValue ** 1
                #xx = replyData["stateOne"]
                updateValueByName(replyData, 'stateOne', str(computedValue)) 
            elif expr == 'exprTwo':
                computedValue = numericalValue ** 2
                updateValueByName(replyData, 'stateTwo', str(computedValue)) 
            elif expr == 'exprThree':
                computedValue = numericalValue ** 3
                updateValueByName(replyData, 'stateThree', str(computedValue)) 
            elif expr == 'exprFour':
                computedValue = numericalValue ** 4
                updateValueByName(replyData, 'stateFour', str(computedValue)) 
            else:
                status = f"FAIL:Expression={expr}"
                #esponseItem = [exprName, f'Argument={exprName} unknown']
                
            #outItem = [stringName, str(squaredValue)]
            #replyData.append(outItem)
            
        except Exception as e:
            status = f"FAIL:DecodeError={e}" 
        
    # Create the returned packet
    contextString = inPacket.context_string
    requestString = inPacket.request_string # we don't change anything
    replyString = json.dumps(replyData)

    outPacket = IpcPacket(sequence_number=inPacket.sequence_number, action=status, context_string=contextString, request_string=requestString, reply_string=replyString)
    return outPacket

# Update the value in a dictionary by name lookup
def updateReplyValueByName(pairs_dict, target_name, new_value):
    if target_name in pairs_dict:
        pairs_dict[target_name].value = new_value
        
def updateValueByName(pair_data, target_name, new_value):
    for item in pair_data:
        if item['name'] == target_name:
            item['value'] = new_value
            return True  # Key found and value updated
    return False  # Key not found in any dictionary

def logit(msg):
    print(msg)
    return


#========================================================================
# Assume name,integer tuples and return the value raised to the 'n' power
def do_get2(inPacket):
    
    logit("Handling do_get2")
    # Decode request as tuples of string,int
    contextData = json.loads(inPacket.context_string)
    requestData = json.loads(inPacket.request_string)    
    replyData = json.loads(inPacket.reply_string)
    
    status = "OK"
    
    # for this example, just make a copy of the indata, but assume
    # that the value is an integer, so square the value
    # replyDict = {item.Name: item for item in inPacket.reply}
    nPower = 1 # Default

    # First get the power we are raise all others to.
    for item in requestData:
        try:
            expr = item["name"]
            if ( expr == "nPower"):
                nPower = int(item("name"))
            
        except Exception as e:
            status = f"FAIL:DecodeError={e}" 
    
    # traverse the request data
    for item in requestData:
        try:
            expr = item["name"]
            intValue = int(item["value"])
            computedValue = 0
            if expr == 'exprOne':
                computedValue = intValue ** nPower
                #xx = replyData["stateOne"]
                updateValueByName(replyData, 'stateOne', str(computedValue)) 
            elif expr == 'exprTwo':
                computedValue = intValue ** nPower
                updateValueByName(replyData, 'stateTwo', str(computedValue)) 
            elif expr == 'exprThree':
                computedValue = intValue ** nPower
                updateValueByName(replyData, 'stateThree', str(computedValue)) 
            elif expr == 'exprFour':
                computedValue = intValue ** nPower
                updateValueByName(replyData, 'stateThree', str(computedValue)) 
            else:
                status = f"FAIL:Expression={expr}"
                #esponseItem = [exprName, f'Argument={exprName} unknown']
                
            #outItem = [stringName, str(squaredValue)]
            #replyData.append(outItem)
            
        except Exception as e:
            status = f"FAIL:DecodeError={e}" 
        
    # Create the returned packet
    replyString = json.dumps(replyData)

    outPacket = IpcPacket(sequence_number=inPacket.sequence_number, action=status, context_string=inPacket.context_string, request_string=inPacket.context_string, reply_string=replyString)
    return outPacket

#========================================================================
# Assume name,integer tuples and return squared integers
def do_getSolarData(inPacket):
    
    logit("Handling do_get1")
    # Decode request as tuples of string,int
    requestData = json.loads(inPacket.request_string)    
    replyData = json.loads(inPacket.reply_string)
    
    status = "OK"
    
    # for this example, just make a copy of the indata, but assume
    # that the value is an integer, so square the value
    # replyDict = {item.Name: item for item in inPacket.reply}
    
    for item in requestData:
        try:
            expr = item["name"]
            intValue = int(item["value"])
            computedValue = 0
            
            if expr == 'exprOne':
                computedValue = intValue ** 2
                #xx = replyData["stateOne"]
                updateValueByName(replyData, 'stateOne', str(computedValue)) 
            elif expr == 'exprTwo':
                computedValue = intValue ** 2
                updateValueByName(replyData, 'stateTwo', str(computedValue)) 
            elif expr == 'exprThree':
                computedValue = intValue ** 2
                updateValueByName(replyData, 'stateThree', str(computedValue)) 
            else:
                status = "FAIL"
                #esponseItem = [exprName, f'Argument={exprName} unknown']
                
            #outItem = [stringName, str(squaredValue)]
            #replyData.append(outItem)
            
        except Exception as e:
            status = f"FAIL:DecodeError={e}" 
        
    # Create the returned packet
    requestString = inPacket.request_string # we don't change anything
    replyString = json.dumps(replyData)

    outPacket = IpcPacket(sequence_number=inPacket.sequence_number, action=status, request_string=inPacket.request_string, reply_string=replyString)
    return outPacket

def getValueByName(pairData, name):
    """
    Returns the value associated with a given name in pairData, 
    which can be a single dictionary or a list of dictionaries.

    :param pairData: A dictionary with a single name/value pair or 
                     a list of dictionaries with name/value pairs.
    :param name: The name key to search for in pairData.
    :return: The value associated with name, or None if not found.
    """
    # If pairData is a dictionary, check directly
    if isinstance(pairData, dict):
        return pairData.get(name, None)

    # If pairData is a list, iterate through each dictionary
    elif isinstance(pairData, list):
        for pair in pairData:
            if name in pair:
                return pair[name]
    
    # Return None if name not found
    return None

# Search through the pair_data and upon finding target_name update with new value        
def updateValueByName(pair_data, target_name, new_value):
    for item in pair_data:
        if item['name'] == target_name:
            item['value'] = new_value
            return True  # Key found and value updated
    return False  # Key not found in any dictionary

def logit(msg):
    print(msg)
    return

def convert_to_number(s):
    # your existing method can be more robust; this is fine for the example
    try:
        if s is None:
            return None
        if isinstance(s, (int, float)):
            return float(s)
        return float(str(s).strip())
    except Exception:
        return None


def handle_do_get1(inPacket):
    request_payload = loads_inner_json_text(inPacket.request_string)
    reply_payload   = loads_inner_json_text(inPacket.reply_string)  # optional template

    out_reply_list = []

    for name, value in iter_name_value(request_payload):
        dd = convert_to_number(value)
        if dd is None:
            out_val = "??"
        else:
            if name == "exprOne":
                out_val = f"{dd ** 1:.2f}"
            elif name == "exprTwo":
                out_val = f"{dd ** 2:.2f}"
            elif name == "exprThree":
                out_val = f"{dd ** 3:.2f}"
            elif name == "exprFour":
                out_val = f"{dd ** 4:.2f}"
            else:
                out_val = "-999.99"

        out_reply_list.append({"name": name, "value": out_val})

    # write inner JSON text back into the packet
    inPacket.reply_string = json.dumps(out_reply_list, separators=(",", ":"))
    inPacket.status = "Success"
    inPacket.action = "SUCCESS"
    return inPacket

import json

def loads_inner_json_text(s):
    """
    Accepts:
      - None / "" -> None
      - '[{"name":"x","value":"1"}]' (single-encoded)
      - '"[{\\"name\\":\\"x\\",\\"value\\":\\"1\\"}]"' (double-encoded)
      - '{"Value1":"10"}' legacy dict (single-encoded)
    Returns: dict/list/None
    """
    if s is None:
        return None
    s = s.strip()
    if s == "":
        return None

    x = json.loads(s)
    if isinstance(x, str):          # double-encoded inner json text
        x = json.loads(x)
    return x


def iter_name_value(payload):
    """
    Normalizes payload into (name,value) pairs.
    Accepts:
      - dict: {"exprOne":"10"}
      - list of dicts: [{"name":"exprOne","value":"10"}] or [{"Name":"exprOne","Value":"10"}]
      - list of pairs: [["exprOne","10"], ...]
    """
    if payload is None:
        return []

    if isinstance(payload, dict):
        return payload.items()

    if isinstance(payload, list):
        if not payload:
            return []

        first = payload[0]
        if isinstance(first, dict):
            def gen():
                for d in payload:
                    name = d.get("name", d.get("Name"))
                    value = d.get("value", d.get("Value"))
                    if name is None:
                        raise KeyError(f"Missing name/Name in item: {d}")
                    yield name, value
            return gen()

        if isinstance(first, (list, tuple)) and len(first) == 2:
            return ((a, b) for a, b in payload)

    raise TypeError(f"Unexpected payload type: {type(payload)}")
