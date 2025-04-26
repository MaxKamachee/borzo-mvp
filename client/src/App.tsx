import React, { useState, useEffect, useRef } from 'react';
import './App.css';
import { motion } from 'framer-motion';
import { Button } from './components/ui/button';
import { Input } from './components/ui/input';

interface ParamRow {
  label: string;
  value: string | number;
}

interface DRCViolation {
  face_id: string;
  rule: string;
  value: number;
}

interface DRCData {
  violations: DRCViolation[];
}

const verdictColor = (verdict: string) => {
  if (verdict.includes('green')) return 'green';
  if (verdict.includes('yellow')) return 'orange';
  return 'red';
};

async function callAgent(text: string) {
  try {
    const res = await fetch('http://localhost:8000/agent', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text }),
    });
    return await res.json();
  } catch (e) {
    return { intent: 'error', agent_response: 'Backend not reachable', action_result: null };
  }
}

function App() {
  const [chat, setChat] = useState([
    { sender: 'borzo', text: 'Hi! I am Borzo. How can I help you design today?' }
  ]);
  const [input, setInput] = useState('');
  const [params, setParams] = useState<ParamRow[]>([]);
  const [verdict, setVerdict] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(false);
  const [propOptions, setPropOptions] = useState<any[]>([]);
  const chatWindowRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (chatWindowRef.current) {
      chatWindowRef.current.scrollTop = chatWindowRef.current.scrollHeight;
    }
  }, [chat]);

  const handleSend = async () => {
    if (!input.trim()) return;
    const userText = input;
    setChat(c => [...c, { sender: 'user', text: input }]);
    await fetch('http://localhost:8000/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sender: 'user', text: userText })
    });
    setInput('');
    setLoading(true);

    // classify natural-language intent
    const classifyRes = await fetch('http://localhost:8000/classify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: input }),
    });
    const classifyData = await classifyRes.json() as { intent: string; params: any; agent_response: string };

    // return early on plain chat or help to avoid duplicate echo/instructions
    if (classifyData.intent === 'chat' || classifyData.intent === 'help') {
      setChat(c => [...c, { sender: 'borzo', text: classifyData.agent_response }]);
      await fetch('http://localhost:8000/log', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sender: 'borzo', text: classifyData.agent_response })
      });
      setLoading(false);
      return;
    }

    // show agent's summary
    setChat(c => [...c, { sender: 'borzo', text: classifyData.agent_response }]);
    await fetch('http://localhost:8000/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sender: 'borzo', text: classifyData.agent_response })
    });

    let botMessage = '';
    switch (classifyData.intent) {
      case 'airfoil': {
        const nacaVal = (classifyData.params?.naca as string) || input;
        const chordVal = (classifyData.params?.chord as number) ?? 200;
        // validate naca code
        if (!/^\d{4}$/.test(nacaVal)) {
          botMessage = `Please provide a valid 4-digit NACA code (e.g. 2412).`;
          setStatus('Invalid NACA code.'); setVerdict('red');
          break;
        }
        // helper to fetch airfoil coords from backend
        const fetchAirfoil = async () => {
          const res = await fetch('http://localhost:8000/airfoil', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ naca: nacaVal, chord: chordVal })
          });
          if (!res.ok) {
            let errBody;
            try { errBody = await res.json(); } catch { errBody = null; }
            throw new Error(errBody?.detail || res.statusText);
          }
          return await res.json() as { coords: any[] };
        };
        let airfoilData: { coords: any[] };
        if ((window as any).external?.GenerateAirfoil) {
          try {
            (window as any).external.GenerateAirfoil(nacaVal, chordVal);
            botMessage = `Airfoil sketch for NACA ${nacaVal} sent to SolidWorks.`;
            setStatus('Airfoil sketch generated.'); setVerdict('green');
            setParams([{ label: 'NACA', value: nacaVal }, { label: 'Chord (mm)', value: chordVal }]);
          } catch (err) {
            console.error('GenerateAirfoil COM failed:', err);
            try {
              airfoilData = await fetchAirfoil();
              botMessage = `Fetched ${airfoilData.coords.length} points for NACA ${nacaVal}.`;
              setStatus('Airfoil data fetched.'); setVerdict('green');
              setParams([{ label: 'NACA', value: nacaVal }, { label: 'Chord (mm)', value: chordVal }]);
            } catch (fetchErr: any) {
              console.error('Airfoil HTTP failed:', fetchErr);
              botMessage = `Error: ${fetchErr.message}`;
              setStatus('Error generating airfoil.'); setVerdict('red'); setParams([]); setPropOptions([]);
              break;
            }
          }
        } else {
          try {
            airfoilData = await fetchAirfoil();
            botMessage = `Fetched ${airfoilData.coords.length} points for NACA ${nacaVal}.`;
            setStatus('Airfoil data fetched.'); setVerdict('green');
            setParams([{ label: 'NACA', value: nacaVal }, { label: 'Chord (mm)', value: chordVal }]);
          } catch (fetchErr: any) {
            console.error('Airfoil HTTP failed:', fetchErr);
            botMessage = `Error: ${fetchErr.message}`;
            setStatus('Error generating airfoil.'); setVerdict('red'); setParams([]); setPropOptions([]);
            break;
          }
        }
        setPropOptions([]);
        break;
      }
      case 'propulsion': {
        // prepare propulsion payload
        const propParams = classifyData.params || {};
        const auwVal = propParams.auw ?? 0;
        const durationVal = propParams.duration_min ?? 0;
        // validate propulsion parameters
        if (auwVal <= 0 || durationVal <= 0) {
          botMessage = `Please specify valid AUW (g) and duration_min (min).`;
          setStatus('Missing propulsion parameters.'); setVerdict('red');
          break;
        }
        const propPayload = { auw: auwVal, duration_min: durationVal };
        const res = await fetch('http://localhost:8000/propulsion', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(propPayload) });
        const data = await res.json() as { options: any[] };
        botMessage = 'Here are propulsion options.';
        setPropOptions(data.options); setStatus('Select a propulsion option.'); setParams([]); setVerdict('');
        break;
      }
      case 'cg': {
        // validate parts list
        const partsList = (classifyData.params?.parts as string[]) || [];
        if (!partsList.length) {
          botMessage = `Please provide parts to check CG, e.g. 'Check CG for parts wing, fuselage'.`;
          setStatus('Missing parts.'); setVerdict('red');
          break;
        }
        let data;
        if ((window as any).external && typeof (window as any).external.GetCG === 'function') {
          try {
            const json = (window as any).external.GetCG();
            data = JSON.parse(json) as { cg_ok: boolean; delta_mm: number; verdict: string };
          } catch (err) {
            console.error('GetCG COM failed:', err);
            const res = await fetch('http://localhost:8000/cg', {
              method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({})
            });
            data = await res.json() as { cg_ok: boolean; delta_mm: number; verdict: string };
          }
        } else {
          const res = await fetch('http://localhost:8000/cg', {
            method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({})
          });
          data = await res.json() as { cg_ok: boolean; delta_mm: number; verdict: string };
        }
        botMessage = 'CG check complete.';
        setStatus('CG check complete.'); setVerdict(data.verdict);
        setParams([{ label: 'CG Delta (mm)', value: data.delta_mm }]);
        setPropOptions([]);
        break;
      }
      case 'drc': {
        // validate part_id
        const partId = (classifyData.params?.part_id as string) || '';
        if (!partId) {
          botMessage = `Please specify a part_id for DRC, e.g. 'Run DRC on part_id wing_spar'.`;
          setStatus('Missing part_id.'); setVerdict('red');
          break;
        }
        let drcData: DRCData;
        if ((window as any).external && typeof (window as any).external.CheckDRC === 'function') {
          try {
            const json = (window as any).external.CheckDRC(partId);
            drcData = JSON.parse(json) as DRCData;
          } catch (err) {
            console.error('CheckDRC COM failed:', err);
            const res = await fetch('http://localhost:8000/drc', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ part_id: partId }) });
            drcData = await res.json() as DRCData;
          }
        } else {
          const res = await fetch('http://localhost:8000/drc', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ part_id: partId }) });
          drcData = await res.json() as DRCData;
        }
        botMessage = 'DRC check complete.';
        setStatus('DRC check complete.');
        setVerdict(drcData.violations.length ? 'yellow' : 'green');
        setParams([]);
        setPropOptions([]);
        setChat(c => [...c, { sender: 'borzo', text: 'Violations: ' + drcData.violations.map(v => `${v.rule} on ${v.face_id} (${v.value}mm)`).join(', ') }]);
        break;
      }
      case 'help': {
        // display usage instructions from classifier
        botMessage = classifyData.agent_response;
        setStatus(''); setVerdict(''); setParams([]); setPropOptions([]);
        break;
      }
      default: {
        // chat fallback
        const chatRes = await callAgent(input);
        botMessage = chatRes.agent_response;
        setStatus(''); setParams([]); setVerdict(''); setPropOptions([]);
      }
    }

    setChat(c => [...c, { sender: 'borzo', text: botMessage }]);
    await fetch('http://localhost:8000/log', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ sender: 'borzo', text: botMessage })
    });
    setLoading(false);
  };

  const handlePropSelect = (option: any) => {
    // Insert STEP into SolidWorks via COM
    try {
      (window as any).external.InsertStep(option.step_file);
    } catch (err) {
      console.error('InsertStep failed:', err);
    }
    setParams([
      { label: 'Propulsion', value: option.name },
      { label: 'Thrust (g)', value: option.thrust_g },
      { label: 'Mass (g)', value: option.mass_g }
    ]);
    setStatus(`Inserted ${option.name}.`);
    setVerdict('green');
    setPropOptions([]);
    setChat(c => [...c, { sender: 'borzo', text: `Inserted ${option.name} into design.` }]);
  };

  return (
    <div className="app-container">
      <div className="split-panel">
        <div className="upper-panel">
          <div
            className="chat-window"
            ref={chatWindowRef}
          >
            {chat.map((msg, i) => (
              <motion.div
                key={i}
                className={msg.sender === 'user' ? 'chat-user' : 'chat-borzo'}
                initial={{ opacity: 0, y: 16 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ duration: 0.22, ease: 'easeOut' }}
              >
                <b>{msg.sender === 'user' ? 'You' : 'Borzo'}:</b> {msg.text}
              </motion.div>
            ))}
            {loading && <div className="chat-borzo"><i>Borzo is thinking...</i></div>}
          </div>
          <div className="status-bar">
            <span>{status}</span>
            {verdict && <span style={{ color: verdictColor(verdict), fontWeight: 'bold', marginLeft: 12 }}>{verdict}</span>}
          </div>
          <form
            className="chat-input-row w-full"
            style={{marginTop: 0, padding: 0, background: 'transparent', border: 'none', boxShadow: 'none'}}
            onSubmit={e => { e.preventDefault(); handleSend(); }}
            autoComplete="off"
          >
            <div className="flex-1">
              <Input
                type="text"
                value={input}
                onChange={e => setInput(e.target.value)}
                placeholder="Type here"
                disabled={loading}
                className="w-full bg-neutral-950 text-white placeholder:text-neutral-500 border border-neutral-800 rounded-l-xl px-5 py-3 text-base focus-visible:ring-2 focus-visible:ring-white focus-visible:border-white transition-all"
                style={{fontFamily: 'Sora, Inter, Segoe UI, Roboto, Arial, sans-serif', borderRight: 'none', boxShadow: 'none'}}
                autoFocus
                spellCheck={false}
                autoComplete="off"
              />
            </div>
            <Button
              type="submit"
              className="chat-send-btn rounded-r-xl h-12 px-8 bg-white text-neutral-950 font-extrabold uppercase tracking-wide border border-neutral-800 border-l-0 shadow-none hover:bg-neutral-200 focus-visible:ring-2 focus-visible:ring-white transition-all"
              disabled={loading || !input.trim()}
              style={{fontFamily: 'Sora, Inter, Segoe UI, Roboto, Arial, sans-serif', marginLeft: 0}}
            >
              Send
            </Button>
          </form>
        </div>
        <div className="lower-panel">
          <h4>Parameters</h4>
          <table className="param-table">
            <tbody>
              {params.map((row, i) => (
                <tr key={i}>
                  <td>{row.label}</td>
                  <td>{row.value}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {propOptions.length > 0 && (
            <div className="prop-options">
              <h4>Propulsion Options</h4>
              {propOptions.map((opt, i) => (
                <button key={i} onClick={() => handlePropSelect(opt)}>{opt.name} ({opt.thrust_g}g, {opt.mass_g}g)</button>
              ))}
            </div>
          )}
          <div className="action-buttons">
            <button style={{ background: '#4caf50', color: '#fff' }}>Approve</button>
            <button style={{ background: '#ff9800', color: '#fff' }}>Modify</button>
            <button style={{ background: '#f44336', color: '#fff' }}>Rollback</button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default App;
