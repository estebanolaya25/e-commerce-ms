import { Component, OnInit } from '@angular/core';
import { TIENDA } from '../../../../core/config/tienda.config';
import { Carrito as CarritoModelo, ItemCarrito } from '../../../../core/modelos/carrito.model';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { CarritoService } from '../../../../core/servicios/carrito.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

@Component({
  selector: 'app-carrito',
  standalone: false,
  templateUrl: './carrito.html',
  styleUrl: './carrito.scss'
})
export class Carrito implements OnInit {
  carrito: CarritoModelo | null = null;
  cargando = true;
  error: string | null = null;
  // SKU con una operación en curso: sus botones se desactivan para evitar clics repetidos.
  ocupados = new Set<string>();
  vaciando = false;

  readonly maximoPorProducto = TIENDA.maximoPorProducto;

  readonly etiquetasEstado: Record<string, { texto: string; clase: string; icono: string }> = {
    PocasUnidades: { texto: 'Pocas unidades', clase: 'advertencia', icono: 'bi-exclamation-circle' },
    StockInsuficiente: { texto: 'Stock insuficiente', clase: 'peligro', icono: 'bi-exclamation-triangle' },
    Agotado: { texto: 'Agotado', clase: 'peligro', icono: 'bi-x-circle' },
    NoDisponible: { texto: 'Ya no está a la venta', clase: 'peligro', icono: 'bi-slash-circle' },
    SinInformacion: { texto: 'Sin información', clase: 'neutro', icono: 'bi-question-circle' }
  };

  constructor(private carritoService: CarritoService, private avisos: AvisosService) {}

  ngOnInit(): void {
    this.carritoService.cargar().subscribe({
      next: carrito => {
        this.carrito = carrito;
        this.cargando = false;
      },
      error: error => {
        this.error = mensajeDeError(error, 'No se pudo cargar el carrito.');
        this.cargando = false;
      }
    });
  }

  get envio(): number {
    return this.carrito && this.carrito.total >= TIENDA.envioGratisDesde ? 0 : TIENDA.costoEnvio;
  }

  get faltaParaEnvioGratis(): number {
    return Math.max(0, TIENDA.envioGratisDesde - (this.carrito?.total ?? 0));
  }

  get progresoEnvioGratis(): number {
    return Math.min(100, ((this.carrito?.total ?? 0) / TIENDA.envioGratisDesde) * 100);
  }

  opcionesTexto(item: ItemCarrito): string {
    return Object.values(item.opciones).join(' · ');
  }

  maximo(item: ItemCarrito): number {
    return Math.min(this.maximoPorProducto, item.stockDisponible ?? this.maximoPorProducto);
  }

  cambiarCantidad(item: ItemCarrito, delta: number): void {
    const nueva = item.cantidad + delta;
    if (nueva < 1) {
      this.quitar(item);
      return;
    }
    this.ejecutar(item.sku, this.carritoService.actualizarCantidad(item.sku, nueva));
  }

  quitar(item: ItemCarrito): void {
    this.ejecutar(item.sku, this.carritoService.quitar(item.sku), () =>
      this.avisos.info('Producto eliminado', item.nombre ?? item.sku)
    );
  }

  vaciar(): void {
    this.vaciando = true;
    this.carritoService.vaciar().subscribe({
      next: () => {
        this.carrito = { ...this.carrito!, items: [], unidades: 0, productosDistintos: 0, total: 0, puedeComprar: false, avisos: [] };
        this.vaciando = false;
      },
      error: error => {
        this.avisos.error('No se pudo vaciar el carrito', mensajeDeError(error));
        this.vaciando = false;
      }
    });
  }

  private ejecutar(sku: string, operacion: ReturnType<CarritoService['quitar']>, alTerminar?: () => void): void {
    this.ocupados.add(sku);
    operacion.subscribe({
      next: carrito => {
        this.carrito = carrito;
        this.ocupados.delete(sku);
        alTerminar?.();
      },
      error: error => {
        this.ocupados.delete(sku);
        this.avisos.error('No se pudo actualizar el carrito', mensajeDeError(error));
      }
    });
  }
}
