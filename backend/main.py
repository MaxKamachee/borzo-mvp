from dotenv import load_dotenv
load_dotenv()
from fastapi import FastAPI, Request, HTTPException
from pydantic import BaseModel
from typing import List, Optional
from datetime import datetime
from fastapi.middleware.cors import CORSMiddleware
import os
import requests
import json
from pathlib import Path
import re
import numpy as np

try:
    from airfoils import Airfoil
    from airfoils.airfoils import NACADefintionError
except ImportError:
    Airfoil = None
    NACADefintionError = Exception

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# --- Models ---
class IntentRequest(BaseModel):
    text: str

class IntentResponse(BaseModel):
    intent: str
    confidence: float

class AirfoilRequest(BaseModel):
    naca: str
    chord: Optional[float] = None

class AirfoilPoint(BaseModel):
    x: float
    y: float

class AirfoilResponse(BaseModel):
    family: str
    params: dict
    coords: List[AirfoilPoint]

class PropulsionRequest(BaseModel):
    auw: float
    duration_min: float

class PropulsionOption(BaseModel):
    name: str
    thrust_g: float
    mass_g: float
    step_file: str

class PropulsionResponse(BaseModel):
    options: List[PropulsionOption]

class CGRequest(BaseModel):
    # Accept missing body by defaulting to empty list
    parts: List[dict] = []

class CGResponse(BaseModel):
    cg_ok: bool
    delta_mm: float
    verdict: str

class DRCRequest(BaseModel):
    # Accept missing body by defaulting to empty string
    part_id: str = ""

class DRCViolation(BaseModel):
    face_id: str
    rule: str
    value: float

class DRCResponse(BaseModel):
    violations: List[DRCViolation]

class AgentRequest(BaseModel):
    text: str

class AgentResponse(BaseModel):
    intent: str
    params: dict
    agent_response: str
    action_result: Optional[dict] = None

OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
OPENAI_CHAT_URL = "https://api.openai.com/v1/chat/completions"

# Data file paths for propulsion catalog and DRC rules
DATA_DIR = Path(__file__).resolve().parent / "data"
PROP_CATALOG_PATH = DATA_DIR / "propulsion_catalog.json"
DRC_RULES_PATH = DATA_DIR / "drc_rules.json"

def openai_agentic_classify(user_message: str):
    system_prompt = (
        "You are Borzo, an AI engineering agent for drone design in SolidWorks.\n"
        "Classify the user's message as one of: airfoil, propulsion, cg, drc, help, or chat.\n"
        "Extract relevant parameters if applicable.\n"
        "Format your response as pure JSON with keys 'intent', 'params', and 'agent_response'.\n"
        "Examples:\n"
        "User: Generate a NACA2412 airfoil with 150 mm chord\n"
        "{\"intent\":\"airfoil\",\"params\":{\"naca\":\"2412\",\"chord\":150},\"agent_response\":\"Generating NACA2412 airfoil with 150 mm chord.\"}\n"
        "User: What propulsion options for AUW=350g and duration=20min?\n"
        "{\"intent\":\"propulsion\",\"params\":{\"auw\":350,\"duration_min\":20},\"agent_response\":\"Here are propulsion options for AUW 350g and 20 min.\"}\n"
        "User: Check CG for parts wing and fuselage\n"
        "{\"intent\":\"cg\",\"params\":{\"parts\":[\"wing\",\"fuselage\"]},\"agent_response\":\"Checking CG for wing and fuselage.\"}\n"
        "User: Run DRC on part tail_spar\n"
        "{\"intent\":\"drc\",\"params\":{\"part_id\":\"tail_spar\"},\"agent_response\":\"Running DRC on part tail_spar.\"}\n"
        "User: How are you today?\n"
        "{\"intent\":\"chat\",\"params\":{},\"agent_response\":\"Hello! How can I assist with your design today?\"}\n"
    )
    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": user_message}
    ]
    headers = {
        "Authorization": f"Bearer {OPENAI_API_KEY}",
        "Content-Type": "application/json"
    }
    data = {
        "model": "gpt-3.5-turbo",
        "messages": messages,
        "max_tokens": 200,
        "temperature": 0
    }
    try:
        resp = requests.post(OPENAI_CHAT_URL, headers=headers, json=data, timeout=10)
        resp.raise_for_status()
        content = resp.json()["choices"][0]["message"]["content"]
        print(f"[Classify] raw LLM content: {content}")
        # strip JSON fences
        clean = re.sub(r"^```(?:json)?\\s*|\\s*```$", "", content.strip())
        import json as pyjson
        try:
            result = pyjson.loads(clean)
            print(f"[Classify] parsed JSON: {result}")
            return result
        except Exception as e:
            print(f"[Classify] JSON parse error: {e}")
            # fall through to regex fallback
    except Exception as err:
        print(f"[Classify] LLM request failed: {err}")
        txt = user_message.lower()
        if "naca" in txt or "airfoil" in txt:
            m = re.search(r"(\d{4})", user_message)
            code = m.group(1) if m else ""
            cm = re.search(r"chord.*?(\d+)", user_message, flags=re.IGNORECASE)
            chord = int(cm.group(1)) if cm else 200
            resp = {"intent":"airfoil","params":{"naca":code,"chord":chord},"agent_response":f"Parsed NACA code: {code}, chord: {chord} mm"}
            print(f"[Fallback] airfoil: {resp}")
            return resp
        if "propulsion" in txt or "motor" in txt:
            am = re.search(r"(\d+)\s*g", user_message, flags=re.IGNORECASE)
            auw = int(am.group(1)) if am else 350
            dm = re.search(r"(\d+)\s*(?:min|m)", user_message, flags=re.IGNORECASE)
            duration = int(dm.group(1)) if dm else 10
            resp = {"intent":"propulsion","params":{"auw":auw,"duration_min":duration},"agent_response":f"Selecting propulsion for AUW={auw} g, duration={duration} min"}
            print(f"[Fallback] propulsion: {resp}")
            return resp
        if "cg" in txt:
            pm = re.search(r"for (.+)", user_message, flags=re.IGNORECASE)
            parts = []
            if pm:
                parts = [p.strip() for p in re.split(r",| and | & ", pm.group(1))]
            resp = {"intent":"cg","params":{"parts":parts},"agent_response":f"Checking CG for parts: {parts}"}
            print(f"[Fallback] cg: {resp}")
            return resp
        if "drc" in txt:
            dm = re.search(r"(?:part_?id|part) ['\"]?(\w+)['\"]?", user_message, flags=re.IGNORECASE)
            pid = dm.group(1) if dm else "demo"
            resp = {"intent":"drc","params":{"part_id":pid},"agent_response":f"Running DRC on part: {pid}"}
            print(f"[Fallback] drc: {resp}")
            return resp
        help_text = "Usage:\n/airfoil NACA_CODE [--chord MM]\n/propulsion --auw G --duration MIN\n/cg --parts part1,part2\n/drc --part_id ID"
        resp = {"intent":"help","params":{},"agent_response":help_text}
        print(f"[Fallback] help: {resp}")
        return resp

# --- Endpoints ---
@app.get("/")
def read_root():
    return {"Borzo": "Backend online"}

@app.post("/log")
async def log_action(request: Request):
    data = await request.json()
    print(f"LOG: {data} @ {datetime.now().isoformat()}")
    return {"status": "logged"}

@app.post("/classify", response_model=AgentResponse)
async def classify_intent(req: AgentRequest):
    # use agentic classification when API key is set
    if OPENAI_API_KEY:
        result = openai_agentic_classify(req.text)
        return AgentResponse(intent=result["intent"], params=result["params"], agent_response=result["agent_response"])
    # simple fallback
    txt = req.text.lower()
    if "naca" in txt or "airfoil" in txt:
        match = re.search(r"(\d{4,5})", req.text)
        naca_code = match.group(1) if match else ""
        intent = "airfoil"
        params = {"naca": naca_code}
        agent_response = f"Parsed NACA code: {naca_code}"
    elif "propulsion" in txt or "motor" in txt:
        intent = "propulsion"
        params = {"auw": 350, "duration_min": 10}
        agent_response = "Selecting propulsion."
    elif "cg" in txt:
        intent = "cg"
        params = {"parts": []}
        agent_response = "Checking CG."
    elif "drc" in txt:
        intent = "drc"
        params = {"part_id": "demo"}
        agent_response = "Running DRC."
    else:
        intent = "chat"
        params = {}
        agent_response = "Echo: " + req.text
    return AgentResponse(intent=intent, params=params, agent_response=agent_response)

@app.post("/airfoil", response_model=AirfoilResponse)
async def generate_airfoil(req: AirfoilRequest):
    chord = req.chord or 200.0
    raw_naca = req.naca
    # expect exactly 4-digit NACA codes
    m = re.fullmatch(r"(\d{4})", raw_naca)
    if not m:
        raise HTTPException(status_code=400, detail=f"Invalid NACA code: {raw_naca}")
    naca_code = m.group(1)
    family = 'NACA 4-digit'
    coords: List[dict] = []
    if Airfoil is None:
        # no airfoils library: return empty coords
        return AirfoilResponse(family=family, params={"naca": naca_code, "chord": chord}, coords=coords)
    try:
        foil = Airfoil.NACA4(naca_code)
    except NACADefintionError:
        raise HTTPException(status_code=400, detail=f"Invalid NACA code: {naca_code}")
    # sample points
    n_pts = 50
    x_norm = np.linspace(0, 1, n_pts)
    y_up = foil.y_upper(x=x_norm)
    y_lo = foil.y_lower(x=x_norm)
    for xi, yi in zip(x_norm, y_up): coords.append({"x": xi * chord, "y": yi * chord})
    for xi, yi in zip(x_norm[::-1], y_lo[::-1]): coords.append({"x": xi * chord, "y": yi * chord})
    return AirfoilResponse(family=family, params={"naca": naca_code, "chord": chord}, coords=coords)

@app.post("/propulsion", response_model=PropulsionResponse)
async def get_propulsion(req: PropulsionRequest):
    # Load propulsion options from JSON catalog
    with open(PROP_CATALOG_PATH) as f:
        data = json.load(f)
    options = [PropulsionOption(**opt) for opt in data.get("options", [])]
    return PropulsionResponse(options=options)

@app.post("/cg", response_model=CGResponse)
async def check_cg(req: CGRequest):
    return CGResponse(cg_ok=True, delta_mm=0.0, verdict="green")

@app.post("/drc", response_model=DRCResponse)
async def check_drc(req: DRCRequest):
    # Load DRC violations from JSON rules based on part_id
    with open(DRC_RULES_PATH) as f:
        drc_rules = json.load(f)
    viols = drc_rules.get(req.part_id, [])
    violations = [DRCViolation(**v) for v in viols]
    return DRCResponse(violations=violations)

@app.post("/agent", response_model=AgentResponse)
async def agentic_action(req: AgentRequest):
    user_message = req.text
    # simple keyword-based classification (MVP)
    txt = user_message.lower()
    if "naca" in txt or "airfoil" in txt:
        match = re.search(r"(\d{4,5})", req.text)
        naca_code = match.group(1) if match else ""
        result = {"intent": "airfoil", "params": {"naca": naca_code}, "agent_response": f"Parsed NACA code: {naca_code}"}
    elif "propulsion" in txt or "motor" in txt:
        result = {"intent": "propulsion", "params": {"auw": 350, "duration_min": 10}, "agent_response": "Selecting propulsion."}
    elif "cg" in txt:
        result = {"intent": "cg", "params": {"parts": []}, "agent_response": "Checking CG."}
    elif "drc" in txt:
        result = {"intent": "drc", "params": {"part_id": "demo"}, "agent_response": "Running DRC."}
    else:
        result = {"intent": "chat", "params": {}, "agent_response": "Echo: " + user_message}
    # Launch workflow if action
    action_result = None
    if result["intent"] == "airfoil":
        params = result["params"]
        family = 'NACA 4-digit' if len(params.get("naca", "")) == 4 else 'NACA 5-digit'
        action_result = {"family": family, "params": {"naca": params.get("naca", "2412"), "chord": params.get("chord", 200), "thickness": 12}}
    elif result["intent"] == "propulsion":
        action_result = {"options": [
            {"name": "T-Motor 2820", "thrust_g": 900, "mass_g": 85, "step_file": "t-motor-2820.step"},
            {"name": "Emax 2216", "thrust_g": 700, "mass_g": 65, "step_file": "emax-2216.step"}
        ]}
    elif result["intent"] == "cg":
        action_result = {"cg_ok": True, "delta_mm": 0, "verdict": "green"}
    elif result["intent"] == "drc":
        action_result = {"violations": [
            {"face_id": "F123", "rule": "min-wall", "value": 1.0},
            {"face_id": "F456", "rule": "hole-edge", "value": 3.0}
        ]}
    # For chat, action_result remains None
    return {
        "intent": result["intent"],
        "params": result["params"],
        "agent_response": result["agent_response"],
        "action_result": action_result
    }
