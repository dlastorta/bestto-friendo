import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TournamentDrafts } from './TournamentDrafts'
import App from '../../App'

afterEach(() => vi.unstubAllGlobals())
const game = { id: 'game', name: 'Juego Azul', minimumFactions: 1, maximumFactions: 1, factions: [{ id: 'blue', name: 'Azul' }] }
const draft = { id: 'draft', name: 'Copa local', gameId: 'game', status: 'Draft' }
function response(body: unknown, status = 200) { return { ok: status < 400, status, json: async () => body } }

describe('torneos en borrador', () => {
  it('crea con CSRF, lista, filtra y consulta el borrador', async () => {
    let exists = false
    const requests = vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/games') return response([game])
      if (url === '/api/auth/csrf') return response({ token: 'csrf' })
      if (url === '/api/tournaments' && options?.method === 'POST') { exists = true; return response(draft, 201) }
      if (url === '/api/tournaments/draft') return response(draft)
      return response(exists ? [draft] : [])
    })
    vi.stubGlobal('fetch', requests)
    render(<TournamentDrafts onAccessLost={vi.fn()} />)
    await screen.findByText('No hay torneos para este filtro.')
    fireEvent.change(screen.getByLabelText('Nombre del torneo'), { target: { value: draft.name } })
    fireEvent.change(screen.getByLabelText('Juego del torneo'), { target: { value: game.id } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear torneo' }))
    expect(await screen.findByRole('heading', { name: 'Detalle del torneo' })).toBeInTheDocument()
    expect(requests).toHaveBeenCalledWith('/api/tournaments', expect.objectContaining({
      method: 'POST', credentials: 'same-origin', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
      body: JSON.stringify({ name: draft.name, gameId: game.id }),
    }))
    expect(screen.getByLabelText('Nombre del torneo')).toHaveValue('')
    fireEvent.change(screen.getByLabelText('Filtrar torneos por juego'), { target: { value: game.id } })
    fireEvent.click(await screen.findByRole('button', { name: draft.name }))
    expect(await screen.findByRole('heading', { name: 'Detalle del torneo' })).toBeInTheDocument()
    expect(requests).toHaveBeenCalledWith('/api/tournaments?gameId=game', expect.anything())
    expect(requests).toHaveBeenCalledWith('/api/tournaments/draft', expect.anything())
    const detail = screen.getByRole('article', { name: 'Detalle del torneo' })
    expect(within(detail).getByText('Borrador')).toBeInTheDocument()
    expect(within(detail).getByText('Privado')).toBeInTheDocument()
    expect(within(detail).getByText('Juego Azul')).toBeInTheDocument()
  })

  it('conserva el formulario cuando el servidor rechaza el torneo', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/games') return response([game])
      if (url === '/api/auth/csrf') return response({ token: 'csrf' })
      if (options?.method === 'POST') return response({ errors: { tournament: ['El juego no existe.'] } }, 400)
      return response([])
    }))
    render(<TournamentDrafts onAccessLost={vi.fn()} />)
    await screen.findByText('No hay torneos para este filtro.')
    fireEvent.change(screen.getByLabelText('Nombre del torneo'), { target: { value: draft.name } })
    fireEvent.change(screen.getByLabelText('Juego del torneo'), { target: { value: game.id } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear torneo' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('El juego no existe.')
    expect(screen.getByLabelText('Nombre del torneo')).toHaveValue(draft.name)
    expect(screen.getByLabelText('Juego del torneo')).toHaveValue(game.id)
  })

  it.each([401, 403])('retira los borradores al perder acceso (%s) y revalida el perfil', async (status) => {
    let lost = false
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url === '/api/me') return response({ id: 'player', displayName: 'Ana', canManageCatalog: false, canOrganizeTournaments: !lost }, lost && status === 401 ? 401 : 200)
      if (url === '/api/games') return response([game])
      if (url === '/api/tournaments/draft') { lost = true; return response({}, status) }
      if (url === '/api/tournaments') return response([draft])
      if (url.startsWith('/api/decks')) return response([])
      return response({ status: 'ok' })
    }))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: draft.name }))
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Mis torneos' })).not.toBeInTheDocument())
    await screen.findByRole('heading', { name: status === 401 ? 'Tu cuenta de jugador' : 'Mi perfil' })
    expect(screen.queryByRole('button', { name: draft.name })).not.toBeInTheDocument()
  })

  it('no ofrece crear torneos a un administrador del catálogo sin rol de organizador', async () => {
    const requests = vi.fn(async (url: string) => {
      if (url === '/api/me') return response({ id: 'player', displayName: 'Ana', canManageCatalog: true, canOrganizeTournaments: false })
      if (url === '/api/games' || url.startsWith('/api/decks')) return response([])
      return response({ status: 'ok' })
    })
    vi.stubGlobal('fetch', requests)
    render(<App />)
    await screen.findByRole('heading', { name: 'Mi perfil' })
    expect(screen.queryByRole('button', { name: 'Crear torneo' })).not.toBeInTheDocument()
    expect(requests.mock.calls.some(([url]) => url.startsWith('/api/tournaments'))).toBe(false)
  })
})
