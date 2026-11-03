import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { Pagina } from '../modelos/catalogo.model';
import { MovimientoStock, StockSku, VistaStock } from '../modelos/inventario.model';

export interface FiltroStock {
  pagina: number;
  tamano: number;
  sku?: string;
  vista?: VistaStock;
}

// Endpoints de bodega del microservicio Inventario (Administrator y Warehouse).
@Injectable({ providedIn: 'root' })
export class AdminInventarioService {
  private readonly base = `${API.inventario}/stock`;

  constructor(private http: HttpClient) {}

  listar(filtro: FiltroStock): Observable<Pagina<StockSku>> {
    let parametros = new HttpParams().set('pagina', filtro.pagina).set('tamano', filtro.tamano);
    if (filtro.sku) parametros = parametros.set('sku', filtro.sku.trim().toUpperCase());
    if (filtro.vista === 'bajo-minimo') parametros = parametros.set('soloBajoMinimo', true);
    if (filtro.vista === 'agotados') parametros = parametros.set('soloAgotados', true);
    return this.http.get<Pagina<StockSku>>(this.base, { params: parametros });
  }

  obtener(sku: string): Observable<StockSku> {
    return this.http.get<StockSku>(`${this.base}/${encodeURIComponent(sku)}`);
  }

  movimientos(sku: string, limite = 50): Observable<MovimientoStock[]> {
    return this.http.get<MovimientoStock[]>(`${this.base}/${encodeURIComponent(sku)}/movimientos`, { params: { limite } });
  }

  // Entrada: siempre suma. Ajuste: suma o resta (no puede dejar el físico por debajo de lo reservado).
  registrarEntrada(sku: string, cantidad: number, motivo: string): Observable<StockSku> {
    return this.http.post<StockSku>(`${this.base}/${encodeURIComponent(sku)}/entradas`, { cantidad, motivo: motivo.trim() });
  }

  registrarAjuste(sku: string, cantidad: number, motivo: string): Observable<StockSku> {
    return this.http.post<StockSku>(`${this.base}/${encodeURIComponent(sku)}/ajustes`, { cantidad, motivo: motivo.trim() });
  }

  actualizarMinimo(sku: string, stockMinimo: number): Observable<StockSku> {
    return this.http.put<StockSku>(`${this.base}/${encodeURIComponent(sku)}/minimo`, { stockMinimo });
  }
}
