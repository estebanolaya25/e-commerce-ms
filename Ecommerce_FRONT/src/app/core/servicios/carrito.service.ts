import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable, distinctUntilChanged, map, tap } from 'rxjs';
import { API } from '../config/api.config';
import { Carrito } from '../modelos/carrito.model';
import { SesionService } from './sesion.service';

// Guarda el último estado del carrito para que la navbar y las páginas lo compartan.
// Cada operación devuelve el carrito actualizado desde el backend, así nunca queda desincronizado.
@Injectable({ providedIn: 'root' })
export class CarritoService {
  private readonly carritoSubject = new BehaviorSubject<Carrito | null>(null);
  readonly carrito$ = this.carritoSubject.asObservable();
  readonly unidades$: Observable<number> = this.carrito$.pipe(map(carrito => carrito?.unidades ?? 0));

  constructor(private http: HttpClient, sesion: SesionService) {
    // Al iniciar sesión se carga el carrito del usuario; al cerrarla se olvida.
    sesion.usuario$
      .pipe(
        map(usuario => usuario?.id ?? null),
        distinctUntilChanged()
      )
      .subscribe(usuarioId => {
        if (usuarioId) {
          this.cargar().subscribe({ error: () => this.carritoSubject.next(null) });
        } else {
          this.carritoSubject.next(null);
        }
      });
  }

  cargar(): Observable<Carrito> {
    return this.http.get<Carrito>(API.carrito).pipe(tap(carrito => this.carritoSubject.next(carrito)));
  }

  agregar(sku: string, cantidad: number): Observable<Carrito> {
    return this.http.post<Carrito>(`${API.carrito}/items`, { sku, cantidad }).pipe(tap(carrito => this.carritoSubject.next(carrito)));
  }

  actualizarCantidad(sku: string, cantidad: number): Observable<Carrito> {
    return this.http
      .put<Carrito>(`${API.carrito}/items/${encodeURIComponent(sku)}`, { cantidad })
      .pipe(tap(carrito => this.carritoSubject.next(carrito)));
  }

  quitar(sku: string): Observable<Carrito> {
    return this.http
      .delete<Carrito>(`${API.carrito}/items/${encodeURIComponent(sku)}`)
      .pipe(tap(carrito => this.carritoSubject.next(carrito)));
  }

  vaciar(): Observable<void> {
    return this.http.delete<void>(API.carrito).pipe(
      tap(() => this.carritoSubject.next({ items: [], productosDistintos: 0, unidades: 0, total: 0, puedeComprar: false, informacionCompleta: true, avisos: [] }))
    );
  }
}
