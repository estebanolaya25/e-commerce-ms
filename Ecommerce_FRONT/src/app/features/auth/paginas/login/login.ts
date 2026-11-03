import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../../core/servicios/auth.service';
import { mensajeDeError } from '../../../../core/utilidades/errores-http';

@Component({
  selector: 'app-login',
  standalone: false,
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login implements OnInit {
  readonly formulario;
  enviando = false;
  verClave = false;
  error: string | null = null;
  aviso: string | null = null;

  readonly cuentasDemo = [
    { etiqueta: 'Cliente', email: 'camila.salazar@ejemplo.com', password: 'Demo12345' },
    { etiqueta: 'Administrador', email: 'admin@ejemplo.com', password: 'Demo12345' }
  ];

  volverA = '/';

  constructor(
    fb: FormBuilder,
    private authService: AuthService,
    private router: Router,
    private ruta: ActivatedRoute
  ) {
    this.formulario = fb.nonNullable.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', Validators.required]
    });
  }

  ngOnInit(): void {
    const parametros = this.ruta.snapshot.queryParamMap;
    this.volverA = parametros.get('volver') ?? '/';
    if (parametros.get('motivo') === 'sesion-expirada') {
      this.aviso = 'Su sesión expiró. Ingrese de nuevo para continuar.';
    } else if (parametros.has('volver')) {
      this.aviso = 'Ingrese para continuar.';
    }
  }

  usarCuentaDemo(email: string, password: string): void {
    this.formulario.setValue({ email, password });
  }

  invalido(campo: 'email' | 'password'): boolean {
    const control = this.formulario.controls[campo];
    return control.invalid && (control.dirty || control.touched);
  }

  ingresar(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.enviando = true;
    this.error = null;
    this.authService.login(this.formulario.getRawValue()).subscribe({
      next: () => this.router.navigateByUrl(this.volverA),
      error: error => {
        this.error = mensajeDeError(error, 'No se pudo iniciar sesión.');
        this.enviando = false;
      }
    });
  }
}
