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
    responseData = json.loads(inPacket.response_string)
    
    status = "OK"
    
    # Get the expression key name and the value (which is an integer in this case)
    # Modify the value and then place the new value into the responseData.
    simTime = getValueByName(contextData, 'SimTime')
    
    for item in requestData:
        try:
            expr = item["name"]
            value = item["value"]
            numericalValue = convert_to_number(value)
            computedValue = 0
            if expr == 'exprOne':
                computedValue = numericalValue ** 1
                #xx = responseData["stateOne"]
                updateValueByName(responseData, 'stateOne', str(computedValue)) 
            elif expr == 'exprTwo':
                computedValue = numericalValue ** 2
                updateValueByName(responseData, 'stateTwo', str(computedValue)) 
            elif expr == 'exprThree':
                computedValue = numericalValue ** 3
                updateValueByName(responseData, 'stateThree', str(computedValue)) 
            elif expr == 'exprFour':
                computedValue = numericalValue ** 4
                updateValueByName(responseData, 'stateFour', str(computedValue)) 
            else:
                status = f"FAIL:Expression={expr}"
                #esponseItem = [exprName, f'Argument={exprName} unknown']
                
            #outItem = [stringName, str(squaredValue)]
            #responseData.append(outItem)
            
        except Exception as e:
            status = f"FAIL:DecodeError={e}" 
        
    # Create the returned packet
    contextString = inPacket.context_string
    requestString = inPacket.request_string # we don't change anything
    responseString = json.dumps(responseData)

    outPacket = IpcPacket(sequence_number=inPacket.sequence_number, action=status, context_string=contextString, request_string=requestString, response_string=responseString)
    return outPacket

# Update the value in a dictionary by name lookup
def updateResponseValueByName(pairs_dict, target_name, new_value):
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
    responseData = json.loads(inPacket.response_string)
    
    status = "OK"
    
    # for this example, just make a copy of the indata, but assume
    # that the value is an integer, so square the value
    # responseDict = {item.Name: item for item in inPacket.response}
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
                #xx = responseData["stateOne"]
                updateValueByName(responseData, 'stateOne', str(computedValue)) 
            elif expr == 'exprTwo':
                computedValue = intValue ** nPower
                updateValueByName(responseData, 'stateTwo', str(computedValue)) 
            elif expr == 'exprThree':
                computedValue = intValue ** nPower
                updateValueByName(responseData, 'stateThree', str(computedValue)) 
            elif expr == 'exprFour':
                computedValue = intValue ** nPower
                updateValueByName(responseData, 'stateThree', str(computedValue)) 
            else:
                status = f"FAIL:Expression={expr}"
                #esponseItem = [exprName, f'Argument={exprName} unknown']
                
            #outItem = [stringName, str(squaredValue)]
            #responseData.append(outItem)
            
        except Exception as e:
            status = f"FAIL:DecodeError={e}" 
        
    # Create the returned packet
    responseString = json.dumps(responseData)

    outPacket = IpcPacket(sequence_number=inPacket.sequence_number, action=status, context_string=inPacket.context_string, request_string=inPacket.context_string, response_string=responseString)
    return outPacket

#========================================================================
# Assume name,integer tuples and return squared integers
def do_getSolarData(inPacket):
    
    logit("Handling do_get1")
    # Decode request as tuples of string,int
    requestData = json.loads(inPacket.request_string)    
    responseData = json.loads(inPacket.response_string)
    
    status = "OK"
    
    # for this example, just make a copy of the indata, but assume
    # that the value is an integer, so square the value
    # responseDict = {item.Name: item for item in inPacket.response}
    
    for item in requestData:
        try:
            expr = item["name"]
            intValue = int(item["value"])
            computedValue = 0
            
            if expr == 'exprOne':
                computedValue = intValue ** 2
                #xx = responseData["stateOne"]
                updateValueByName(responseData, 'stateOne', str(computedValue)) 
            elif expr == 'exprTwo':
                computedValue = intValue ** 2
                updateValueByName(responseData, 'stateTwo', str(computedValue)) 
            elif expr == 'exprThree':
                computedValue = intValue ** 2
                updateValueByName(responseData, 'stateThree', str(computedValue)) 
            else:
                status = "FAIL"
                #esponseItem = [exprName, f'Argument={exprName} unknown']
                
            #outItem = [stringName, str(squaredValue)]
            #responseData.append(outItem)
            
        except Exception as e:
            status = f"FAIL:DecodeError={e}" 
        
    # Create the returned packet
    requestString = inPacket.request_string # we don't change anything
    responseString = json.dumps(responseData)

    outPacket = IpcPacket(sequence_number=inPacket.sequence_number, action=status, request_string=inPacket.request_string, response_string=responseString)
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


