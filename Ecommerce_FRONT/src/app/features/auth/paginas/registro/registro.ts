import { Component, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, ValidationErrors, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../../core/servicios/auth.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

// Misma política que exige el backend (Auth): 8+ caracteres, mayúscula, minúscula y número.
function politicaClave(control: AbstractControl): ValidationErrors | null {
  const valor: string = control.value ?? '';
  const valida = valor.length >= 8 && /[A-Z]/.test(valor) && /[a-z]/.test(valor) && /[0-9]/.test(valor);
  return valida ? null : { politica: true };
}

function clavesIguales(grupo: AbstractControl): ValidationErrors | null {
  return grupo.get('password')?.value === grupo.get('confirmacion')?.value ? null : { noCoinciden: true };
}

@Component({
  selector: 'app-registro',
  standalone: false,
  templateUrl: './registro.html',
  styleUrl: './registro.scss'
})
export class Registro implements OnInit {
  readonly formulario;
  enviando = false;
  verClave = false;
  error: string | null = null;
  volverA = '/';

  constructor(
    fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private ruta: ActivatedRoute
  ) {
    this.formulario = fb.nonNullable.group(
      {
        nombre: ['', [Validators.required, Validators.maxLength(100)]],
        apellido: ['', [Validators.required, Validators.maxLength(100)]],
        email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
        password: ['', [Validators.required, politicaClave]],
        confirmacion: ['', Validators.required]
      },
      { validators: clavesIguales }
    );
  }

  ngOnInit(): void {
    this.volverA = this.ruta.snapshot.queryParamMap.get('volver') ?? '/';
  }

  get clave(): string {
    return this.formulario.controls.password.value;
  }

  get reglasClave() {
    const valor = this.clave;
    return [
      { texto: 'Mínimo 8 caracteres', cumple: valor.length >= 8 },
      { texto: 'Una mayúscula', cumple: /[A-Z]/.test(valor) },
      { texto: 'Una minúscula', cumple: /[a-z]/.test(valor) },
      { texto: 'Un número', cumple: /[0-9]/.test(valor) }
    ];
  }

  invalido(campo: 'nombre' | 'apellido' | 'email' | 'password' | 'confirmacion'): boolean {
    const control = this.formulario.controls[campo];
    return control.invalid && (control.dirty || control.touched);
  }

  get confirmacionNoCoincide(): boolean {
    const confirmacion = this.formulario.controls.confirmacion;
    return this.formulario.hasError('noCoinciden') && (confirmacion.dirty || confirmacion.touched);
  }

  registrar(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }

    const { nombre, apellido, email, password } = this.formulario.getRawValue();
    this.enviando = true;
    this.error = null;
    this.authService.registrar({ nombre: nombre.trim(), apellido: apellido.trim(), email: email.trim(), password }).subscribe({
      next: () => this.router.navigateByUrl(this.volverA),
      error: error => {
        this.error = mensajeDeError(error, 'No se pudo crear la cuenta.');
        this.enviando = false;
      }
    });
  }
}
