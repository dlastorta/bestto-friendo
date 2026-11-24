import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { GamesCatalog } from './GamesCatalog'

afterEach(() => vi.unstubAllGlobals())

describe('Slice 01 · Catálogo', () => {
  it('permite crear un juego y mostrar las facciones devueltas por la API', async () => {
    const fetchMock = vi.fn(async (_url: string, options?: RequestInit) => {
      if (_url === '/api/auth/csrf') return { ok: true, status: 200, json: async () => ({ token: 'test-csrf' }) }
      if (options?.method === 'POST') {
        return {
          ok: true,
          status: 201,
          json: async () => ({
            id: 'game-1', name: 'L5R', minimumFactions: 1, maximumFactions: 1,
            factions: [{ id: 'a', name: 'León' }, { id: 'b', name: 'Grulla' }],
          }),
        }
      }
      return { ok: true, status: 200, json: async () => [] }
    })
    vi.stubGlobal('fetch', fetchMock)

    render(<GamesCatalog canCreate />)
    await screen.findByText('Todavía no hay juegos')

    fireEvent.change(screen.getByLabelText('Nombre del juego'), { target: { value: 'L5R' } })
    fireEvent.change(screen.getByLabelText('Facciones o colores'), { target: { value: 'León\nGrulla' } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear juego' }))

    await waitFor(() => {
      expect(screen.getByText('León')).toBeInTheDocument()
      expect(screen.getByText('Grulla')).toBeInTheDocument()
    })
    expect(fetchMock).toHaveBeenCalledWith('/api/games', expect.objectContaining({
      method: 'POST',
      body: JSON.stringify({ name: 'L5R', factions: ['León', 'Grulla'], minimumFactions: 1, maximumFactions: 1 }),
    }))
  })

  it('expone errores de validación del backend sin agregar el juego', async () => {
    vi.stubGlobal('fetch', vi.fn(async (_url: string, options?: RequestInit) => _url === '/api/auth/csrf'
      ? { ok: true, status: 200, json: async () => ({ token: 'test-csrf' }) } : options?.method === 'POST'
      ? { ok: false, status: 400, json: async () => ({ errors: { game: ['Facciones repetidas.'] } }) }
      : { ok: true, status: 200, json: async () => [] }))
    render(<GamesCatalog canCreate />)
    await screen.findByText('Todavía no hay juegos')

    fireEvent.change(screen.getByLabelText('Nombre del juego'), { target: { value: 'Magic' } })
    fireEvent.change(screen.getByLabelText('Facciones o colores'), { target: { value: 'Azul, Azul' } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear juego' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Facciones repetidas.')
    expect(screen.getByText('Todavía no hay juegos')).toBeInTheDocument()
  })

  it('muestra reglas de selección múltiple de juegos existentes', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true, status: 200, json: async () => [{
        id: 'game-1', name: 'Magic', minimumFactions: 1, maximumFactions: 5,
        factions: [{ id: 'a', name: 'Blanco' }, { id: 'b', name: 'Azul' }],
      }],
    })))
    render(<GamesCatalog />)

    expect(await screen.findByText('1 a 5 facciones')).toBeInTheDocument()
    expect(screen.getByText('Blanco')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Crear juego' })).not.toBeInTheDocument()
  })
})
