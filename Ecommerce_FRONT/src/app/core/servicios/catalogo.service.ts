import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import { Categoria, FiltroProductos, Marca, Pagina, ProductoDetalle, ProductoResumen, VarianteResumen } from '../modelos/catalogo.model';

@Injectable({ providedIn: 'root' })
export class CatalogoService {
  constructor(private http: HttpClient) {}

  obtenerCategorias(): Observable<Categoria[]> {
    return this.http.get<Categoria[]>(`${API.catalogo}/categorias`);
  }

  obtenerMarcas(): Observable<Marca[]> {
    return this.http.get<Marca[]>(`${API.catalogo}/marcas`);
  }

  listarProductos(filtro: FiltroProductos): Observable<Pagina<ProductoResumen>> {
    let parametros = new HttpParams()
      .set('pagina', filtro.pagina)
      .set('tamano', filtro.tamano)
      .set('orden', filtro.orden);

    if (filtro.categoria) parametros = parametros.set('categoria', filtro.categoria);
    if (filtro.marca) parametros = parametros.set('marca', filtro.marca);
    if (filtro.buscar) parametros = parametros.set('buscar', filtro.buscar);
    if (filtro.precioMin != null) parametros = parametros.set('precioMin', filtro.precioMin);
    if (filtro.precioMax != null) parametros = parametros.set('precioMax', filtro.precioMax);
    if (filtro.soloDestacados) parametros = parametros.set('soloDestacados', true);

    return this.http.get<Pagina<ProductoResumen>>(`${API.catalogo}/productos`, { params: parametros });
  }

  obtenerProducto(slug: string): Observable<ProductoDetalle> {
    return this.http.get<ProductoDetalle>(`${API.catalogo}/productos/${encodeURIComponent(slug)}`);
  }

  // Máximo 50 SKU por consulta (límite del microservicio).
  obtenerVariantesPorSku(skus: string[]): Observable<VarianteResumen[]> {
    return this.http.get<VarianteResumen[]>(`${API.catalogo}/variantes`, { params: { skus: skus.slice(0, 50).join(',') } });
  }
}
