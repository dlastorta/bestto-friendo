import { useEffect, useRef, useState } from 'react'
import { AuthenticationError, csrfHeaders } from '../identity/api'
import { deckRequest, type DeckSummary } from '../decks/api'
import { tournamentRequest, TournamentPermissionError } from './api'
import { Standings } from './Standings'

type Entry = { tournamentId: string; name: string; gameId: string; hasPresentedDeck: boolean }
type PresentedDeck = { name: string; listText: string; sourceRevision: number; number: number; reason: string; presentedAt: string }
type Detail = { tournamentId: string; hasPresentedDeck: boolean; deck: PresentedDeck | null; status: string }
type Invitation = { tournamentId: string; name: string; gameId: string; status: string }

export async function registrationRequest<T>(path = '', body?: unknown): Promise<T> {
  const response = await fetch(`/api/registrations${path}`, {
    method: body === undefined ? 'GET' : 'POST', credentials: 'same-origin', cache: 'no-store',
    headers: body === undefined ? { Accept: 'application/json' } : await csrfHeaders(),
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  })
  if (response.status === 401) throw new AuthenticationError()
  if (!response.ok) {
    const error = await response.json().catch(() => ({})) as { message?: string; errors?: Record<string, string[]> }
    throw new Error(error.message ?? Object.values(error.errors ?? {}).flat()[0] ?? 'No se pudo completar la inscripción.')
  }
  return response.json() as Promise<T>
}

export function TournamentParticipants({ tournamentId, status = 'Draft', onAccessLost }: { tournamentId: string; status?: string; onAccessLost: () => void }) {
  const [link, setLink] = useState('')
  const [participants, setParticipants] = useState<{ playerId: string; displayName: string; hasPresentedDeck: boolean }[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  async function run(action: () => Promise<void>) {
    setBusy(true); setError('')
    try { await action() } catch (cause) {
      if (mounted.current) {
        if (cause instanceof AuthenticationError || cause instanceof TournamentPermissionError) onAccessLost()
        else setError(cause instanceof Error ? cause.message : 'No se pudo cargar.')
      }
    } finally { if (mounted.current) setBusy(false) }
  }
  return <section aria-label="Inscripciones del torneo">
    <h4>Inscripciones</h4>
    <p>Compartí el enlace con los jugadores. Solo ellos pueden ver sus listas.</p>
    {error && <p role="alert">{error}</p>}
    <button disabled={busy || status !== 'Draft'} onClick={() => void run(async () => {
      const result = await tournamentRequest<{ token: string }>(`/${tournamentId}/registration-link`, {})
      if (mounted.current) setLink(`${window.location.origin}/#inscripcion=${result.token}`)
    })}>Obtener enlace de inscripción</button>
    {link && <label>Enlace de inscripción<input readOnly value={link} /></label>}
    <button disabled={busy} onClick={() => void run(async () => {
      const result = await tournamentRequest<typeof participants>(`/${tournamentId}/participants`)
      if (mounted.current) setParticipants(result)
    })}>Consultar inscriptos</button>
    <ul>{participants.map(p => <li key={p.playerId}>{p.displayName} · {p.hasPresentedDeck ? 'Mazo presentado' : 'Mazo pendiente'}</li>)}</ul>
  </section>
}

export function Registrations({ onSessionExpired }: { onSessionExpired: () => void }) {
  const [token, setToken] = useState(() => new URLSearchParams(window.location.hash.slice(1)).get('inscripcion') ?? '')
  const [invitation, setInvitation] = useState<Invitation | null>(null)
  const [entries, setEntries] = useState<Entry[]>([])
  const [selected, setSelected] = useState<Detail | null>(null)
  const [decks, setDecks] = useState<DeckSummary[]>([])
  const [deckId, setDeckId] = useState('')
  const [reason, setReason] = useState('')
  const [history, setHistory] = useState<PresentedDeck[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const mounted = useRef(false)
  useEffect(() => { mounted.current = true; return () => { mounted.current = false } }, [])
  useEffect(() => {
    const changed = () => { setToken(new URLSearchParams(window.location.hash.slice(1)).get('inscripcion') ?? ''); setInvitation(null) }
    window.addEventListener('hashchange', changed)
    return () => window.removeEventListener('hashchange', changed)
  }, [])
  async function run(action: () => Promise<void>) {
    setBusy(true); setError('')
    try { await action() } catch (cause) {
      if (mounted.current) {
        if (cause instanceof AuthenticationError) onSessionExpired()
        else setError(cause instanceof Error ? cause.message : 'No se pudo cargar.')
      }
    } finally { if (mounted.current) setBusy(false) }
  }
  async function refresh() {
    const result = await registrationRequest<Entry[]>()
    if (mounted.current) setEntries(result)
  }
  async function open(entry: Entry) {
    setSelected(null); setDecks([]); setDeckId(''); setReason(''); setHistory([])
    const [detail, available] = await Promise.all([
      registrationRequest<Detail>(`/${entry.tournamentId}`), deckRequest<DeckSummary[]>(`?gameId=${entry.gameId}`),
    ])
    if (mounted.current) { setSelected(detail); setDecks(available) }
  }
  return <section className="tournaments-section" aria-label="Mis inscripciones">
    <h2>Mis inscripciones</h2>
    <p>Podés inscribirte sin mazo. Debés presentarlo antes del inicio; solo vos podés ver su lista.</p>
    {error && <p role="alert">{error}</p>}
    <fieldset disabled={busy}>
      <label>Código del enlace<input value={token} onChange={e => { setToken(e.target.value); setInvitation(null) }} /></label>
      <button disabled={!token} onClick={() => void run(async () => {
        const result = await registrationRequest<Invitation>(`/invite/${encodeURIComponent(token)}`)
        if (mounted.current) setInvitation(result)
      })}>Consultar invitación</button>
      {invitation && <div><h3>{invitation.name}</h3><button disabled={invitation.status !== 'Draft'} onClick={() => void run(async () => {
        await registrationRequest(`/invite/${encodeURIComponent(token)}`, {})
        await refresh()
        if (mounted.current) setInvitation(null)
      })}>Inscribirme</button></div>}
      <button onClick={() => void run(refresh)}>Recargar mis inscripciones</button>
      <ul>{entries.map(entry => <li key={entry.tournamentId}>
        <button onClick={() => void run(() => open(entry))}>{entry.name}</button> · {entry.hasPresentedDeck ? 'Mazo presentado' : 'Mazo pendiente'}
      </li>)}</ul>
      {selected && <article aria-label="Mi presentación">
        {selected.status === 'InProgress' && <Standings key={selected.tournamentId} tournamentId={selected.tournamentId} onAccessLost={onSessionExpired} />}
        {selected.deck && <><h3>{selected.deck.name}</h3><p>Presentación {selected.deck.number} · Revisión de origen: {selected.deck.sourceRevision}</p><pre>{selected.deck.listText}</pre></>}
        {selected.status === 'InProgress' ? <p>El torneo comenzó: la presentación está bloqueada.</p> : <>
          <label>Mazo a presentar<select value={deckId} onChange={e => setDeckId(e.target.value)}>
            <option value="">Elegí un mazo propio</option>{decks.map(d => <option key={d.id} value={d.id}>{d.name}</option>)}
          </select></label>
          <p>Se guardará una copia. Editar tu biblioteca después no la cambia. Los reemplazos conservan las presentaciones anteriores.</p>
          {selected.deck && <label>Motivo del reemplazo<input value={reason} maxLength={500} onChange={e => setReason(e.target.value)} /></label>}
          <button disabled={!deckId || (selected.deck !== null && !reason.trim())} onClick={() => void run(async () => {
            const result = await registrationRequest<Detail>(`/${selected.tournamentId}/deck${selected.deck ? '/replace' : ''}`, {
              deckId, expectedDeckVersion: decks.find(d => d.id === deckId)?.version,
              ...(selected.deck ? { expectedPresentationVersion: selected.deck.number, reason } : {}),
            })
            if (mounted.current) { setSelected(result); setReason(''); setHistory([]) }
            await refresh()
          })}>{selected.deck ? 'Reemplazar presentación' : 'Presentar mazo'}</button>
        </>}
        <button onClick={() => void run(async () => {
          const result = await registrationRequest<PresentedDeck[]>(`/${selected.tournamentId}/deck/history`)
          if (mounted.current) setHistory(result)
        })}>Ver historial de presentaciones</button>
        {history.map(s => <details key={s.number}><summary>Presentación {s.number} · {s.reason}</summary><p>{s.name} · {new Date(s.presentedAt).toLocaleString()}</p><pre>{s.listText}</pre></details>)}
      </article>}
    </fieldset>
  </section>
}
