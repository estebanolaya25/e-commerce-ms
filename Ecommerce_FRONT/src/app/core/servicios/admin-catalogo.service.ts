import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API } from '../config/api.config';
import {
  Categoria, CategoriaPeticion, CrearProductoPeticion, ImagenProducto, Marca, MarcaPeticion,
  Pagina, ProductoDetalle, ProductoPeticion, ProductoResumen, Variante, VariantePeticion
} from '../modelos/catalogo.model';

export interface FiltroProductosAdmin {
  pagina: number;
  tamano: number;
  buscar?: string;
  categoria?: string;
  marca?: string;
}

// Escritura del catálogo (Administrator y CatalogManager). Siempre incluye lo inactivo,
// que la tienda pública no ve.
@Injectable({ providedIn: 'root' })
export class AdminCatalogoService {
  private readonly base = API.catalogo;

  constructor(private http: HttpClient) {}

  // ---- Productos ----
  listarProductos(filtro: FiltroProductosAdmin): Observable<Pagina<ProductoResumen>> {
    let parametros = new HttpParams()
      .set('pagina', filtro.pagina)
      .set('tamano', filtro.tamano)
      .set('orden', 'recientes')
      .set('incluirInactivos', true);
    if (filtro.buscar) parametros = parametros.set('buscar', filtro.buscar);
    if (filtro.categoria) parametros = parametros.set('categoria', filtro.categoria);
    if (filtro.marca) parametros = parametros.set('marca', filtro.marca);
    return this.http.get<Pagina<ProductoResumen>>(`${this.base}/productos`, { params: parametros });
  }

  obtenerProducto(productoId: string): Observable<ProductoDetalle> {
    return this.http.get<ProductoDetalle>(`${this.base}/productos/id/${productoId}`);
  }

  crearProducto(peticion: CrearProductoPeticion): Observable<ProductoDetalle> {
    return this.http.post<ProductoDetalle>(`${this.base}/productos`, peticion);
  }

  actualizarProducto(productoId: string, peticion: ProductoPeticion): Observable<ProductoDetalle> {
    return this.http.put<ProductoDetalle>(`${this.base}/productos/${productoId}`, peticion);
  }

  cambiarEstadoProducto(productoId: string, activo: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/productos/${productoId}/estado`, { activo });
  }

  // ---- Variantes ----
  agregarVariante(productoId: string, peticion: VariantePeticion): Observable<Variante> {
    return this.http.post<Variante>(`${this.base}/productos/${productoId}/variantes`, peticion);
  }

  actualizarVariante(varianteId: string, peticion: VariantePeticion): Observable<Variante> {
    return this.http.put<Variante>(`${this.base}/variantes/${varianteId}`, peticion);
  }

  cambiarEstadoVariante(varianteId: string, activa: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/variantes/${varianteId}/estado`, { activo: activa });
  }

  // ---- Imágenes ----
  subirImagen(productoId: string, archivo: File, esPrincipal: boolean, textoAlternativo?: string): Observable<ImagenProducto> {
    const datos = new FormData();
    datos.append('archivo', archivo);
    datos.append('esPrincipal', String(esPrincipal));
    if (textoAlternativo?.trim()) datos.append('textoAlternativo', textoAlternativo.trim());
    return this.http.post<ImagenProducto>(`${this.base}/productos/${productoId}/imagenes`, datos);
  }

  eliminarImagen(imagenId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/imagenes/${imagenId}`);
  }

  marcarImagenPrincipal(imagenId: string): Observable<void> {
    return this.http.patch<void>(`${this.base}/imagenes/${imagenId}/principal`, {});
  }

  // ---- Categorías ----
  listarCategorias(): Observable<Categoria[]> {
    return this.http.get<Categoria[]>(`${this.base}/categorias`, { params: { incluirInactivas: true } });
  }

  crearCategoria(peticion: CategoriaPeticion): Observable<Categoria> {
    return this.http.post<Categoria>(`${this.base}/categorias`, peticion);
  }

  actualizarCategoria(categoriaId: string, peticion: CategoriaPeticion): Observable<Categoria> {
    return this.http.put<Categoria>(`${this.base}/categorias/${categoriaId}`, peticion);
  }

  cambiarEstadoCategoria(categoriaId: string, activa: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/categorias/${categoriaId}/estado`, { activo: activa });
  }

  // ---- Marcas ----
  listarMarcas(): Observable<Marca[]> {
    return this.http.get<Marca[]>(`${this.base}/marcas`, { params: { incluirInactivas: true } });
  }

  crearMarca(peticion: MarcaPeticion): Observable<Marca> {
    return this.http.post<Marca>(`${this.base}/marcas`, peticion);
  }

  actualizarMarca(marcaId: string, peticion: MarcaPeticion): Observable<Marca> {
    return this.http.put<Marca>(`${this.base}/marcas/${marcaId}`, peticion);
  }

  cambiarEstadoMarca(marcaId: string, activa: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/marcas/${marcaId}/estado`, { activo: activa });
  }
}
