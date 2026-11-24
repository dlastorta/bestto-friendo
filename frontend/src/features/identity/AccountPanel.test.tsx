import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { AccountPanel } from './AccountPanel'

afterEach(() => vi.unstubAllGlobals())

describe('Cuenta de jugador', () => {
  it('registra, inicia sesión, edita el perfil y cierra sesión usando CSRF', async () => {
    let signedIn = false
    let name = 'Ana'
    const requests = vi.fn(async (url: string, options?: RequestInit) => {
      let status = 200
      let body: unknown = {}
      if (url === '/api/auth/csrf') body = { token: 'csrf-test' }
      else if (url === '/api/auth/register') status = 201
      else if (url === '/api/auth/login') { signedIn = true; status = 204 }
      else if (url === '/api/auth/logout') { signedIn = false; status = 204 }
      else if (url === '/api/me') {
        if (options?.method === 'PUT') name = (JSON.parse(options.body as string) as { displayName: string }).displayName
        status = signedIn ? 200 : 401
        body = { id: 'player-1', displayName: name, canManageCatalog: false }
      }
      return { ok: status < 400, status, json: async () => body }
    })
    vi.stubGlobal('fetch', requests)
    const onChange = vi.fn()
    render(<AccountPanel onChange={onChange} />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Registrarme' })).toBeEnabled())
    fireEvent.click(screen.getByRole('button', { name: 'Registrarme' }))
    fireEvent.change(screen.getByLabelText('Nombre del jugador'), { target: { value: 'Ana' } })
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.test' } })
    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'a long password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(await screen.findByRole('heading', { name: 'Mi perfil' })).toBeInTheDocument()
    expect(onChange).toHaveBeenLastCalledWith({ id: 'player-1', displayName: 'Ana', canManageCatalog: false })
    fireEvent.change(screen.getByLabelText('Nombre del jugador'), { target: { value: 'Ana Nueva' } })
    fireEvent.click(screen.getByRole('button', { name: 'Guardar perfil' }))
    expect(await screen.findByText('Perfil actualizado.')).toBeInTheDocument()
    expect(requests).toHaveBeenCalledWith('/api/me', expect.objectContaining({
      method: 'PUT', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'csrf-test' }),
      body: JSON.stringify({ displayName: 'Ana Nueva' }),
    }))
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
    expect(await screen.findByRole('heading', { name: 'Tu cuenta de jugador' })).toBeInTheDocument()
    expect(onChange).toHaveBeenLastCalledWith(null)
  })

  it('muestra un error de login sin abrir un perfil', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => ({
      ok: url === '/api/auth/csrf', status: url === '/api/auth/csrf' ? 200 : 401,
      json: async () => ({ token: 'csrf-test' }),
    })))
    render(<AccountPanel onChange={vi.fn()} />)
    await waitFor(() => expect(screen.getByRole('button', { name: 'Iniciar sesión' })).toBeEnabled())
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'ana@example.test' } })
    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'wrong' } })
    fireEvent.click(screen.getByRole('button', { name: 'Iniciar sesión' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Email o contraseña incorrectos')
    expect(screen.queryByRole('heading', { name: 'Mi perfil' })).not.toBeInTheDocument()
  })
})
