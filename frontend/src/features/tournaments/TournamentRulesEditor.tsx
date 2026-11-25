import { useEffect, useRef, useState, type FormEvent } from 'react'
import { AuthenticationError } from '../identity/api'
import { tournamentRequest, TournamentPermissionError, type Tournament } from './api'

const criteria = [
  { code: 'Buchholz', label: 'Buchholz' },
  { code: 'OMWPercentage', label: 'OMW%' },
  { code: 'BuchholzCut', label: 'Buchholz con descarte' },
]
const pointFields = [
  { key: 'winPoints', label: 'Puntos por victoria' }, { key: 'drawPoints', label: 'Puntos por empate' },
  { key: 'lossPoints', label: 'Puntos por derrota' }, { key: 'byePoints', label: 'Puntos por bye' },
] as const

export function TournamentRulesEditor({ tournament, onSaved, onAccessLost }: {
  tournament: Tournament; onSaved: (value: Tournament) => void; onAccessLost: () => void
}) {
  const [points, setPoints] = useState(() => Object.fromEntries(pointFields.map(({ key }) => [key, tournament.rules?.[key]?.toString() ?? ''])))
  const [byes, setByes] = useState(tournament.rules ? String(tournament.rules.byesEnabled) : '')
  const [order, setOrder] = useState(tournament.rules?.tieBreakers ?? [])
  const [choice, setChoice] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])

  function fail(cause: unknown) {
    if (!mounted.current) return
    if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) { onAccessLost(); return }
    setError(cause instanceof Error ? cause.message : 'No se pudieron guardar las reglas.')
  }

  async function save(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try {
      const result = await tournamentRequest<Tournament>(`/${tournament.id}/rules`, {
        ...Object.fromEntries(pointFields.map(({ key }) => [key, points[key] === '' ? null : Number(points[key])])),
        byesEnabled: byes === '' ? null : byes === 'true', tieBreakers: order, expectedVersion: tournament.rulesVersion,
      }, 'PUT')
      if (mounted.current) onSaved(result)
    } catch (cause) { fail(cause) }
    finally { if (mounted.current) setBusy(false) }
  }

  async function reload() {
    setBusy(true); setError('')
    try {
      const result = await tournamentRequest<Tournament>(`/${tournament.id}`)
      if (mounted.current) {
        setPoints(Object.fromEntries(pointFields.map(({ key }) => [key, result.rules?.[key]?.toString() ?? ''])))
        setByes(result.rules ? String(result.rules.byesEnabled) : ''); setOrder(result.rules?.tieBreakers ?? []); setChoice('')
        onSaved(result)
      }
    } catch (cause) { fail(cause) }
    finally { if (mounted.current) setBusy(false) }
  }

  function move(index: number, delta: number) {
    setOrder(current => {
      const updated = [...current]; [updated[index], updated[index + delta]] = [updated[index + delta], updated[index]]
      return updated
    })
  }

  return <section className="tournament-rules" aria-labelledby="rules-heading">
    <h4 id="rules-heading">Reglas del torneo</h4>
    <p role="status">{tournament.rules ? `Configuración guardada · versión ${tournament.rulesVersion}` : 'Sin configurar'}</p>
    {error && <p role="alert" className="form-alert form-alert--error">{error}</p>}
    <form onSubmit={event => void save(event)}>
      <fieldset disabled={busy || tournament.status !== 'Draft'}>
        <legend>Puntuación y bye</legend>
        {pointFields.map(({ key, label }) => <label key={key} htmlFor={`rules-${key}`}>{label}
          <input id={`rules-${key}`} type="number" step="0.01" value={points[key]} required
            onChange={event => setPoints(current => ({ ...current, [key]: event.target.value }))} />
        </label>)}
        <label htmlFor="rules-byes">Política de bye</label>
        <select id="rules-byes" value={byes} required onChange={event => setByes(event.target.value)}>
          <option value="">Elegí una política</option><option value="true">Habilitados</option><option value="false">Deshabilitados</option>
        </select>
        <p>Máximo un bye por participante. Si están deshabilitados, su puntaje queda guardado pero no se otorgan byes.</p>
        <h4>Orden de desempates</h4>
        <p>Se aplicarán en este orden. Las fórmulas se implementarán más adelante; una lista vacía conserva los empates.</p>
        <ol>{order.map((code, index) => {
          const label = criteria.find(item => item.code === code)?.label ?? code
          return <li key={code}><span>{label}</span><div className="rule-actions">
            <button type="button" aria-label={`Subir ${label}`} disabled={index === 0} onClick={() => move(index, -1)}>↑</button>
            <button type="button" aria-label={`Bajar ${label}`} disabled={index === order.length - 1} onClick={() => move(index, 1)}>↓</button>
            <button type="button" aria-label={`Quitar ${label}`} onClick={() => setOrder(current => current.filter(item => item !== code))}>Quitar</button>
          </div></li>
        })}</ol>
        <label htmlFor="rules-criterion">Agregar criterio</label>
        <select id="rules-criterion" value={choice} onChange={event => setChoice(event.target.value)}>
          <option value="">Elegí un criterio</option>{criteria.filter(item => !order.includes(item.code)).map(item => <option key={item.code} value={item.code}>{item.label}</option>)}
        </select>
        <button type="button" disabled={!choice} onClick={() => { setOrder(current => [...current, choice]); setChoice('') }}>Agregar desempate</button>
        <button type="submit">{busy ? 'Guardando…' : 'Guardar reglas'}</button>
      </fieldset>
    </form>
    <button type="button" disabled={busy} onClick={() => void reload()}>Recargar reglas (descarta cambios)</button>
  </section>
}
