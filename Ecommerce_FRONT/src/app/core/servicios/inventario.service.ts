import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { API } from '../config/api.config';
import { Disponibilidad } from '../modelos/inventario.model';

@Injectable({ providedIn: 'root' })
export class InventarioService {
  constructor(private http: HttpClient) {}

  // Inventario acepta hasta 100 SKU por consulta: una sola llamada para todas las variantes de un producto.
  obtenerDisponibilidad(skus: string[]): Observable<Disponibilidad[]> {
    if (skus.length === 0) {
      return of([]);
    }
    const parametros = new HttpParams().set('skus', skus.join(','));
    return this.http.get<Disponibilidad[]>(`${API.inventario}/disponibilidad`, { params: parametros });
  }
}
