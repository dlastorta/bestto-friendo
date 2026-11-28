import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import { Registrations, TournamentParticipants } from './Registrations'

afterEach(() => { vi.unstubAllGlobals(); window.history.replaceState(null, '', '/') })
function response(body: unknown, status = 200) { return { ok: status < 400, status, json: async () => body } }

it('inscribe por enlace sin exigir mazo y permite presentarlo después con CSRF', async () => {
  window.history.replaceState(null, '', '/#inscripcion=token')
  let presented = false
  const request = vi.fn(async (url: string, options?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url === '/api/registrations/invite/token') return response(options?.method === 'POST'
      ? { tournamentId: 'cup', status: 'Draft', hasPresentedDeck: false, deck: null } : { tournamentId: 'cup', name: 'Copa', gameId: 'game', status: 'Draft' })
    if (url === '/api/registrations') return response([{ tournamentId: 'cup', name: 'Copa', gameId: 'game', status: 'Draft', hasPresentedDeck: presented }])
    if (url === '/api/decks?gameId=game') return response([{ id: 'deck', name: 'Propio', version: 3 }])
    if (url === '/api/registrations/cup/deck') { presented = true; return response({ tournamentId: 'cup', status: 'Draft', hasPresentedDeck: true, deck: { name: 'Propio', listText: 'Lista literal', sourceRevision: 2 } }) }
    return response({ tournamentId: 'cup', status: 'Draft', hasPresentedDeck: false, deck: null })
  })
  vi.stubGlobal('fetch', request)
  render(<Registrations onSessionExpired={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar invitación' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Inscribirme' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Copa' }))
  fireEvent.change(await screen.findByLabelText('Mazo a presentar'), { target: { value: 'deck' } })
  fireEvent.click(screen.getByRole('button', { name: 'Presentar mazo' }))
  expect(await screen.findByText('Lista literal')).toBeInTheDocument()
  expect(request).toHaveBeenCalledWith('/api/registrations/cup/deck', expect.objectContaining({
    body: JSON.stringify({ deckId: 'deck', expectedDeckVersion: 3 }), headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
  }))
})

it('muestra al organizador solo el estado de presentación', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => response([{ playerId: 'player', displayName: 'Jugador', hasPresentedDeck: true }])))
  render(<TournamentParticipants tournamentId="cup" onAccessLost={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Consultar inscriptos' }))
  expect(await screen.findByText('Jugador · Mazo presentado')).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: /ver mazo/i })).not.toBeInTheDocument()
})

it('notifica sesión vencida y retira la vista al desmontarse', async () => {
  const expired = vi.fn()
  vi.stubGlobal('fetch', vi.fn(async () => response({}, 401)))
  const view = render(<Registrations onSessionExpired={expired} />)
  fireEvent.click(screen.getByRole('button', { name: 'Recargar mis inscripciones' }))
  await waitFor(() => expect(expired).toHaveBeenCalledOnce())
  view.unmount()
  expect(screen.queryByRole('region', { name: 'Mis inscripciones' })).not.toBeInTheDocument()
})

it('conserva selección ante conflicto de presentación', async () => {
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url === '/api/registrations') return response([{ tournamentId: 'cup', name: 'Copa', gameId: 'game', status: 'Draft', hasPresentedDeck: false }])
    if (url.startsWith('/api/decks')) return response([{ id: 'deck', name: 'Propio', version: 1 }])
    if (url.endsWith('/deck')) return response({ message: 'El mazo cambió. Recargá tu biblioteca.' }, 409)
    return response({ tournamentId: 'cup', status: 'Draft', hasPresentedDeck: false, deck: null })
  }))
  render(<Registrations onSessionExpired={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Recargar mis inscripciones' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Copa' }))
  fireEvent.change(await screen.findByLabelText('Mazo a presentar'), { target: { value: 'deck' } })
  fireEvent.click(screen.getByRole('button', { name: 'Presentar mazo' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('El mazo cambió')
  expect(screen.getByLabelText('Mazo a presentar')).toHaveValue('deck')
})

it('permite autorizar y retirar solo la presentación propia usando versiones y CSRF', async () => {
  let visible = false
  let version = 0
  const detail = () => ({ tournamentId: 'cup', status: 'Finished', hasPresentedDeck: true, deckIsPublic: visible, visibilityVersion: version,
    deck: { name: 'Propio', listText: 'Lista privada', sourceRevision: 1, number: 1 } })
  const request = vi.fn(async (url: string, options?: RequestInit) => {
    if (url === '/api/auth/csrf') return response({ token: 'csrf' })
    if (url === '/api/registrations') return response([{ tournamentId: 'cup', name: 'Copa', gameId: 'game', hasPresentedDeck: true }])
    if (url.startsWith('/api/decks')) return response([])
    if (url.endsWith('/visibility')) { visible = JSON.parse(options!.body as string).visible; version++; return response(detail()) }
    return response(detail())
  })
  vi.stubGlobal('fetch', request)
  render(<Registrations onSessionExpired={vi.fn()} />)
  fireEvent.click(screen.getByRole('button', { name: 'Recargar mis inscripciones' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Copa' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Autorizar publicación de esta presentación' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Retirar autorización pública de mi presentación' }))
  await screen.findByRole('button', { name: 'Autorizar publicación de esta presentación' })
  expect(request).toHaveBeenCalledWith('/api/registrations/cup/deck/visibility', expect.objectContaining({ body: JSON.stringify({ visible: false, snapshotNumber: 1, expectedVersion: 1 }), headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }) }))
})
