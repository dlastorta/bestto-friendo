import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { TournamentRulesEditor } from './TournamentRulesEditor'
import type { Tournament } from './api'

afterEach(() => vi.unstubAllGlobals())
const draft: Tournament = { id: 'draft', name: 'Copa', gameId: 'game', status: 'Draft', rulesVersion: 0, rules: null }
const configured: Tournament = { ...draft, rulesVersion: 1, rules: {
  winPoints: 2.5, drawPoints: 1, lossPoints: 0, byesEnabled: true, byePoints: 2,
  maximumByesPerParticipant: 1, tieBreakers: ['Buchholz', 'OMWPercentage'],
} }
function response(body: unknown, status = 200) { return { ok: status < 400, status, json: async () => body } }

describe('reglas de torneo', () => {
  it('no impone puntajes iniciales y guarda la selección en el orden elegido con CSRF', async () => {
    const onSaved = vi.fn()
    const requests = vi.fn(async (url: string) => response(url === '/api/auth/csrf' ? { token: 'csrf' } : configured))
    vi.stubGlobal('fetch', requests)
    render(<TournamentRulesEditor tournament={draft} onSaved={onSaved} onAccessLost={vi.fn()} />)
    expect(screen.getByText('Sin configurar')).toBeInTheDocument()
    expect(screen.getByLabelText('Puntos por victoria')).toHaveValue(null)
    for (const [label, value] of [['Puntos por victoria', '2.5'], ['Puntos por empate', '1'], ['Puntos por derrota', '0'], ['Puntos por bye', '2']])
      fireEvent.change(screen.getByLabelText(label), { target: { value } })
    fireEvent.change(screen.getByLabelText('Política de bye'), { target: { value: 'true' } })
    for (const code of ['OMWPercentage', 'Buchholz']) {
      fireEvent.change(screen.getByLabelText('Agregar criterio'), { target: { value: code } })
      fireEvent.click(screen.getByRole('button', { name: 'Agregar desempate' }))
    }
    fireEvent.click(screen.getByRole('button', { name: 'Subir Buchholz' }))
    fireEvent.click(screen.getByRole('button', { name: 'Guardar reglas' }))
    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(configured))
    expect(requests).toHaveBeenCalledWith('/api/tournaments/draft/rules', expect.objectContaining({
      method: 'PUT', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf' }),
      body: JSON.stringify({ winPoints: 2.5, drawPoints: 1, lossPoints: 0, byePoints: 2,
        byesEnabled: true, tieBreakers: ['Buchholz', 'OMWPercentage'], expectedVersion: 0 }),
    }))
  })

  it('mantiene cambios ante un conflicto y permite recargar explícitamente', async () => {
    const onSaved = vi.fn()
    vi.stubGlobal('fetch', vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/auth/csrf') return response({ token: 'csrf' })
      if (options?.method === 'PUT') return response({ message: 'Las reglas cambiaron. Recargá antes de guardar.' }, 409)
      return response({ ...configured, rulesVersion: 2 })
    }))
    render(<TournamentRulesEditor tournament={configured} onSaved={onSaved} onAccessLost={vi.fn()} />)
    fireEvent.change(screen.getByLabelText('Puntos por victoria'), { target: { value: '8' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar reglas' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Las reglas cambiaron')
    expect(screen.getByLabelText('Puntos por victoria')).toHaveValue(8)
    expect(onSaved).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'Recargar reglas (descarta cambios)' }))
    await waitFor(() => expect(screen.getByLabelText('Puntos por victoria')).toHaveValue(2.5))
    expect(onSaved).toHaveBeenCalledWith(expect.objectContaining({ rulesVersion: 2 }))
  })

  it.each([401, 403])('notifica pérdida de acceso al guardar (%s)', async status => {
    const onAccessLost = vi.fn()
    vi.stubGlobal('fetch', vi.fn(async (url: string) => url === '/api/auth/csrf' ? response({ token: 'csrf' }) : response({}, status)))
    render(<TournamentRulesEditor tournament={configured} onSaved={vi.fn()} onAccessLost={onAccessLost} />)
    fireEvent.click(screen.getByRole('button', { name: 'Guardar reglas' }))
    await waitFor(() => expect(onAccessLost).toHaveBeenCalledOnce())
  })

  it('deshabilita edición fuera de borrador', () => {
    render(<TournamentRulesEditor tournament={{ ...configured, status: 'InProgress' }} onSaved={vi.fn()} onAccessLost={vi.fn()} />)
    expect(screen.getByLabelText('Puntos por victoria')).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Guardar reglas' })).toBeDisabled()
  })
})
