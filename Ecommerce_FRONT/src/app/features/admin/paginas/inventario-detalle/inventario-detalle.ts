import { Component, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';
import { ROLES_CATALOGO } from '../../../../core/config/roles';
import { VarianteResumen } from '../../../../core/modelos/catalogo.model';
import { MovimientoStock, StockSku, TIPOS_MOVIMIENTO, TipoMovimiento } from '../../../../core/modelos/inventario.model';
import { AdminInventarioService } from '../../../../core/servicios/admin-inventario.service';
import { AvisosService } from '../../../../core/servicios/avisos.service';
import { CatalogoService } from '../../../../core/servicios/catalogo.service';
import { SesionService } from '../../../../core/servicios/sesion.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';
import { fechaUtc } from '../../../../core/utilidades/fechas';

type Operacion = 'entrada' | 'ajuste' | 'minimo';

@Component({
  selector: 'app-inventario-detalle',
  standalone: false,
  templateUrl: './inventario-detalle.html',
  styleUrl: './inventario-detalle.scss'
})
export class InventarioDetalle implements OnInit {
  sku = '';
  stock: StockSku | null = null;
  variante: VarianteResumen | null = null;
  movimientos: MovimientoStock[] = [];
  cargando = true;
  noEncontrado = false;
  operacion: Operacion = 'entrada';
  guardando = false;
  readonly veCatalogo: boolean;

  readonly operaciones: { valor: Operacion; texto: string; icono: string }[] = [
    { valor: 'entrada', texto: 'Entrada', icono: 'bi-box-arrow-in-down' },
    { valor: 'ajuste', texto: 'Ajuste', icono: 'bi-sliders' },
    { valor: 'minimo', texto: 'Stock mínimo', icono: 'bi-bell' }
  ];

  readonly formEntrada;
  readonly formAjuste;
  readonly formMinimo;

  constructor(
    fb: FormBuilder,
    private ruta: ActivatedRoute,
    private adminInventario: AdminInventarioService,
    private catalogo: CatalogoService,
    private avisos: AvisosService,
    sesion: SesionService
  ) {
    this.veCatalogo = sesion.tieneAlgunRol(ROLES_CATALOGO);
    // Mismos límites que valida Inventario.
    this.formEntrada = fb.group({
      cantidad: [null as number | null, [Validators.required, Validators.min(1), Validators.max(100000)]],
      motivo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(250)]]
    });
    this.formAjuste = fb.group({
      cantidad: [null as number | null, [Validators.required, Validators.min(-100000), Validators.max(100000), (c: { value: number | null }) => (c.value === 0 ? { cero: true } : null)]],
      motivo: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(250)]]
    });
    this.formMinimo = fb.nonNullable.group({
      stockMinimo: [0, [Validators.required, Validators.min(0), Validators.max(100000)]]
    });
  }

  ngOnInit(): void {
    this.sku = this.ruta.snapshot.paramMap.get('sku')!.toUpperCase();
    forkJoin({
      stock: this.adminInventario.obtener(this.sku),
      movimientos: this.adminInventario.movimientos(this.sku),
      variante: this.catalogo.obtenerVariantesPorSku([this.sku]).pipe(
        map(v => v[0] ?? null),
        catchError(() => of(null))
      )
    }).subscribe({
      next: ({ stock, movimientos, variante }) => {
        this.aplicarStock(stock);
        this.movimientos = movimientos;
        this.variante = variante;
        this.cargando = false;
      },
      error: () => {
        this.noEncontrado = true;
        this.cargando = false;
      }
    });
  }

  // Lo que quedaría tras el ajuste, para que se vea antes de confirmarlo.
  get fisicoTrasAjuste(): number | null {
    const cantidad = this.formAjuste.controls.cantidad.value;
    return this.stock && cantidad ? this.stock.cantidadFisica + cantidad : null;
  }

  get ajusteInvalido(): boolean {
    const resultado = this.fisicoTrasAjuste;
    return resultado !== null && !!this.stock && resultado < this.stock.cantidadReservada;
  }

  invalido(formulario: 'entrada' | 'ajuste' | 'minimo', campo: string): boolean {
    const grupo: AbstractControl = formulario === 'entrada' ? this.formEntrada : formulario === 'ajuste' ? this.formAjuste : this.formMinimo;
    const control = grupo.get(campo);
    return !!control && control.invalid && control.touched;
  }

  info(tipo: TipoMovimiento) {
    return TIPOS_MOVIMIENTO[tipo] ?? { texto: tipo, icono: 'bi-dot', clase: 'neutro', ayuda: '' };
  }

  fecha(texto: string): Date {
    return fechaUtc(texto);
  }

  registrarEntrada(): void {
    this.formEntrada.markAllAsTouched();
    if (this.formEntrada.invalid) return;
    const { cantidad, motivo } = this.formEntrada.getRawValue();
    this.ejecutar(this.adminInventario.registrarEntrada(this.sku, cantidad!, motivo!), `Entrada de ${cantidad} unidades registrada.`, () => this.formEntrada.reset({ cantidad: null, motivo: '' }));
  }

  registrarAjuste(): void {
    this.formAjuste.markAllAsTouched();
    if (this.formAjuste.invalid || this.ajusteInvalido) return;
    const { cantidad, motivo } = this.formAjuste.getRawValue();
    this.ejecutar(this.adminInventario.registrarAjuste(this.sku, cantidad!, motivo!), `Ajuste de ${cantidad! > 0 ? '+' : ''}${cantidad} aplicado.`, () => this.formAjuste.reset({ cantidad: null, motivo: '' }));
  }

  actualizarMinimo(): void {
    this.formMinimo.markAllAsTouched();
    if (this.formMinimo.invalid) return;
    const { stockMinimo } = this.formMinimo.getRawValue();
    this.ejecutar(this.adminInventario.actualizarMinimo(this.sku, stockMinimo), `El aviso de stock bajo queda en ${stockMinimo} unidades.`, () => {});
  }

  private ejecutar(llamada: ReturnType<AdminInventarioService['obtener']>, mensaje: string, limpiar: () => void): void {
    this.guardando = true;
    llamada.subscribe({
      next: stock => {
        this.aplicarStock(stock);
        limpiar();
        this.guardando = false;
        this.avisos.exito('Inventario actualizado', mensaje);
        this.adminInventario.movimientos(this.sku).subscribe(movimientos => (this.movimientos = movimientos));
      },
      error: error => {
        this.guardando = false;
        this.avisos.error('No se pudo actualizar', mensajeDeError(error));
      }
    });
  }

  private aplicarStock(stock: StockSku): void {
    this.stock = stock;
    this.formMinimo.reset({ stockMinimo: stock.stockMinimo });
  }
}
