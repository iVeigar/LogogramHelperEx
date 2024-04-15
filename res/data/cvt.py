import json
a = []
with open("logosActions.json", "r") as f:
    data = json.load(f)
    for action in data:
        #print(f'{{ {logo["Id"]}, new(){{ Id = {logo["Id"]}, Name = "{logo["Name"]}" }} }},')
        a.append(action["Recipes"])

print(a)