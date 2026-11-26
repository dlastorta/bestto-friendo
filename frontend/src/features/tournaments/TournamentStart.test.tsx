import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { TournamentStart } from './TournamentStart'
import { Registrations } from './Registrations'
import type { Tournament } from './api'
import { TournamentDrafts } from './TournamentDrafts'

afterEach(() => vi.unstubAllGlobals())
const draft: Tournament = { id: 'cup', name: 'Copa', gameId: 'game', status: 'Draft', rulesVersion: 2, rules: null, participationVersion: 4 }
const response = (body: unknown, status = 200) => ({ ok: status < 400, status, json: async () => body })

it('retira la confirmación anterior al iniciar desde el detalle completo', async () => {
  let current = draft
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url === '/api/games') return response([])
    if (url.endsWith('/start')) { current = { ...draft, status: 'InProgress' }; return response(current) }
    if (url === '/api/tournaments/cup') return response(current)
    return response([current])
  }))
  render(<TournamentDrafts onAccessLost={vi.fn()} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Copa' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Comenzar torneo' }))
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar inicio' }))
  await screen.findByText(/Torneo en curso\. Reglas históricas/)
  expect(screen.queryByRole('button', { name: 'Confirmar inicio' })).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Obtener enlace de inscripción' })).toBeDisabled()
  expect(screen.getByRole('button', { name: 'Guardar reglas' })).toBeDisabled()
})

it('exige confirmación y envía versiones observadas con CSRF al iniciar', async () => {
  const saved = vi.fn()
  const request = vi.fn(async (url: string) => response(url === '/api/auth/csrf' ? { token: 'csrf' } : { ...draft, status: 'InProgress' }))
  vi.stubGlobal('fetch', request)
  render(<TournamentStart tournament={draft} onSaved={saved} onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Comenzar torneo' }))
  expect(request).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar inicio' }))
  await waitFor(() => expect(saved).toHaveBeenCalledOnce())
  expect(request).toHaveBeenCalledWith('/api/tournaments/cup/start', expect.objectContaining({
    body: JSON.stringify({ expectedRulesVersion: 2, expectedParticipationVersion: 4 }),
    headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
  }))
})

it('muestra conflicto sin dar por iniciado el torneo', async () => {
  const saved = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async url => url === '/api/auth/csrf' ? response({ token: 'csrf' }) : response({ message: 'El torneo cambió. Recargá.' }, 409)))
  render(<TournamentStart tournament={draft} onSaved={saved} onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Comenzar torneo' }))
  fireEvent.click(screen.getByRole('button', { name: 'Confirmar inicio' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('El torneo cambió')
  expect(saved).not.toHaveBeenCalled()
})

it('muestra el bloqueo después de iniciar', () => {
  render(<TournamentStart tournament={{ ...draft, status: 'InProgress', startedAt: '2026-11-26T10:00:00Z' }} onSaved={vi.fn()} onAccessLost={vi.fn()} />)
  expect(screen.getByRole('status')).toHaveTextContent('Reglas históricas y presentaciones bloqueadas')
  expect(screen.queryByRole('button', { name: 'Comenzar torneo' })).not.toBeInTheDocument()
})

it('reemplaza con motivo y conserva acceso al historial privado', async () => {
  const original = { name: 'Original', listText: 'Lista anterior', number: 1, sourceRevision: 1, reason: 'Presentación inicial', presentedAt: '2026-11-26T10:00:00Z' }
  const replacement = { ...original, name: 'Nuevo', listText: 'Lista nueva', number: 2, reason: 'Cambio elegido' }
  const request = vi.fn(async (url: string) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url === '/api/registrations') return response([{ tournamentId: 'cup', name: 'Copa', gameId: 'game', hasPresentedDeck: true }])
    if (url.startsWith('/api/decks')) return response([{ id: 'new', name: 'Nuevo', version: 3 }])
    if (url.endsWith('/history')) return response([replacement, original])
    return response({ tournamentId: 'cup', status: 'Draft', hasPresentedDeck: true, deck: url.endsWith('/replace') ? replacement : original })
  })
  vi.stubGlobal('fetch', request)
  render(<Registrations onSessionExpired={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Recargar mis inscripciones' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Copa' }))
  fireEvent.change(await screen.findByLabelText('Mazo a presentar'), { target: { value: 'new' } })
  expect(screen.getByRole('button', { name: 'Reemplazar presentación' })).toBeDisabled()
  fireEvent.change(screen.getByLabelText('Motivo del reemplazo'), { target: { value: 'Cambio elegido' } })
  fireEvent.click(screen.getByRole('button', { name: 'Reemplazar presentación' }))
  expect(await screen.findByText('Lista nueva')).toBeInTheDocument()
  expect(request).toHaveBeenCalledWith('/api/registrations/cup/deck/replace', expect.objectContaining({ body: JSON.stringify({ deckId: 'new', expectedDeckVersion: 3, expectedPresentationVersion: 1, reason: 'Cambio elegido' }) }))
  fireEvent.click(screen.getByRole('button', { name: 'Ver historial de presentaciones' }))
  expect(await screen.findByText('Presentación 1 · Presentación inicial')).toBeInTheDocument()
})
