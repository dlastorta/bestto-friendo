import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DeckLibrary } from './DeckLibrary'
import App from '../../App'

afterEach(() => vi.unstubAllGlobals())
const game = { id: 'game', name: 'Juego Azul', minimumFactions: 1, maximumFactions: 1, factions: [{ id: 'blue', name: 'Azul' }] }
const original = { number: 1, name: 'Mazo privado', listText: '  4 x Carta\ntexto sin formato  ', factionIds: ['blue'] }
const initial = { id: 'deck', gameId: 'game', version: 1, isArchived: false, current: original }
function response(body: unknown, status = 200) { return { ok: status < 400, status, json: async () => body } }

describe('biblioteca personal', () => {
  it('crea texto libre, guarda revisiones y archiva sin perder el historial', async () => {
    let deck = initial
    let exists = false
    const history = [original]
    const requests = vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/games') return response([game])
      if (url === '/api/auth/csrf') return response({ token: 'csrf' })
      if (url === '/api/decks' && options?.method === 'POST') {
        const body = JSON.parse(options.body as string)
        deck = { ...initial, current: { ...original, name: body.name, listText: body.listText } }
        history[0] = deck.current; exists = true
        return response(deck, 201)
      }
      if (url === '/api/decks/deck' && options?.method === 'PUT') {
        const body = JSON.parse(options.body as string)
        deck = { ...deck, version: 2, current: { ...deck.current, number: 2, listText: body.listText } }
        history.unshift(deck.current)
        return response(deck)
      }
      if (url === '/api/decks/deck/archive') { deck = { ...deck, isArchived: true, version: 3 }; return response(deck) }
      if (url === '/api/decks/deck/revisions') return response(history)
      if (url === '/api/decks/deck') return response(deck)
      return response(exists && url.includes(`archived=${deck.isArchived}`) ? [{ ...deck, name: deck.current.name, revision: deck.current.number }] : [])
    })
    vi.stubGlobal('fetch', requests)
    render(<DeckLibrary onSessionExpired={vi.fn()} />)
    await within(screen.getByLabelText('Juego del mazo')).findByRole('option', { name: 'Juego Azul' })
    fireEvent.change(screen.getByLabelText('Nombre del mazo'), { target: { value: original.name } })
    fireEvent.change(screen.getByLabelText('Juego del mazo'), { target: { value: game.id } })
    fireEvent.click(screen.getByLabelText('Azul'))
    fireEvent.change(screen.getByLabelText('Lista de cartas'), { target: { value: original.listText } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear mazo' }))
    await screen.findByText('Revisión 1 · Mazo privado')
    expect(requests).toHaveBeenCalledWith('/api/decks', expect.objectContaining({
      headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
      body: JSON.stringify({ gameId: 'game', name: original.name, listText: original.listText, factionIds: ['blue'] }),
    }))
    fireEvent.change(screen.getByLabelText('Lista de cartas'), { target: { value: 'otra lista sin controles' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar revisión' }))
    await screen.findByText('Revisión 2 · Mazo privado')
    expect(screen.getByText('Revisión 1 · Mazo privado').parentElement?.querySelector('pre')?.textContent).toBe(original.listText)
    fireEvent.click(screen.getByRole('button', { name: 'Archivar mazo' }))
    await screen.findByRole('heading', { name: 'Mazo archivado' })
    expect(screen.getByLabelText('Lista de cartas')).toBeDisabled()
    expect(screen.getByText('Revisión 1 · Mazo privado')).toBeInTheDocument()
    fireEvent.click(screen.getByLabelText('Ver archivados'))
    expect(await screen.findByRole('button', { name: 'Mazo privado · revisión 2' })).toBeEnabled()
  })

  it('conserva el borrador cuando el servidor rechaza una versión desactualizada', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/games') return response([game])
      if (url === '/api/auth/csrf') return response({ token: 'csrf' })
      if (options?.method === 'PUT') return response({ message: 'El mazo cambió. Recargá su versión antes de guardar.' }, 409)
      if (url.endsWith('/revisions')) return response([original])
      if (url === '/api/decks/deck') return response(initial)
      return response([{ ...initial, name: original.name, revision: 1 }])
    }))
    render(<DeckLibrary onSessionExpired={vi.fn()} />)
    fireEvent.click(await screen.findByRole('button', { name: 'Mazo privado · revisión 1' }))
    await screen.findByText('Revisión 1 · Mazo privado')
    fireEvent.change(screen.getByLabelText('Lista de cartas'), { target: { value: 'mi borrador' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar revisión' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('El mazo cambió')
    expect(screen.getByLabelText('Lista de cartas')).toHaveValue('mi borrador')
  })

  it('quita biblioteca y perfil de la pantalla cuando vence la sesión', async () => {
    let expired = false
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url === '/api/me') return response({ id: 'player', displayName: 'Ana', canManageCatalog: false }, expired ? 401 : 200)
      if (url === '/api/games') return response([game])
      if (url.startsWith('/api/decks')) {
        if (url === '/api/decks/deck' || url.endsWith('/revisions')) { expired = true; return response({}, 401) }
        return response([{ ...initial, name: original.name, revision: 1 }])
      }
      return response({ status: 'ok' })
    }))
    render(<App />)
    fireEvent.click(await screen.findByRole('button', { name: 'Mazo privado · revisión 1' }))
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Mi biblioteca de mazos' })).not.toBeInTheDocument())
    expect(await screen.findByRole('heading', { name: 'Tu cuenta de jugador' })).toBeInTheDocument()
    expect(screen.queryByText('Mazo privado · revisión 1')).not.toBeInTheDocument()
  })
})
