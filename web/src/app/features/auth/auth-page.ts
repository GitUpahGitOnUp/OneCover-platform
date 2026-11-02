import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { apiErrorMessage } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { RegistrationRequest } from '../../core/auth/auth.models';

@Component({
  selector: 'app-auth-page',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.scss',
})
export class AuthPage {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly isRegister = signal(false);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');
  readonly returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
  readonly latestHireDate = new Date().toISOString().slice(0, 10);
  readonly form = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email, Validators.maxLength(256)],
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(128)],
    }),
    firstName: new FormControl('', { nonNullable: true }),
    lastName: new FormControl('', { nonNullable: true }),
    hireDate: new FormControl('', { nonNullable: true }),
    annualSalary: new FormControl('', { nonNullable: true }),
    employmentType: new FormControl<'FullTime' | 'PartTime'>('FullTime', { nonNullable: true }),
    ssnLast4: new FormControl('', { nonNullable: true }),
  });

  constructor() {
    this.route.data.subscribe((data) => {
      this.isRegister.set(data['mode'] === 'register');
      this.errorMessage.set('');
      this.updateRegistrationValidators();
    });
  }

  submit(): void {
    if (this.form.invalid || this.isSubmitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.errorMessage.set('');
    this.isSubmitting.set(true);
    const values = this.form.getRawValue();
    const request = this.isRegister()
      ? this.auth.register(this.toRegistrationRequest(values))
      : this.auth.login({ email: values.email, password: values.password });

    request.pipe(finalize(() => this.isSubmitting.set(false))).subscribe({
      next: () => void this.router.navigateByUrl(this.getReturnUrl()),
      error: (error: unknown) => this.errorMessage.set(apiErrorMessage(error)),
    });
  }

  private updateRegistrationValidators(): void {
    const registrationValidators = this.isRegister();
    this.form.controls.password.setValidators([
      Validators.required,
      Validators.maxLength(128),
      ...(registrationValidators ? [Validators.minLength(8)] : []),
    ]);
    this.form.controls.firstName.setValidators(
      registrationValidators ? [Validators.required, Validators.maxLength(100)] : [],
    );
    this.form.controls.lastName.setValidators(
      registrationValidators ? [Validators.required, Validators.maxLength(100)] : [],
    );
    this.form.controls.hireDate.setValidators(
      registrationValidators
        ? [
            Validators.required,
            (control) =>
              control.value && control.value > this.latestHireDate ? { futureDate: true } : null,
          ]
        : [],
    );
    this.form.controls.annualSalary.setValidators(
      registrationValidators
        ? [Validators.required, Validators.pattern(/^\d+(\.\d{1,2})?$/), Validators.max(10000000)]
        : [],
    );
    this.form.controls.ssnLast4.setValidators(
      registrationValidators ? [Validators.required, Validators.pattern(/^\d{4}$/)] : [],
    );
    for (const control of Object.values(this.form.controls)) {
      control.updateValueAndValidity({ emitEvent: false });
    }
  }

  private toRegistrationRequest(
    values: ReturnType<typeof this.form.getRawValue>,
  ): RegistrationRequest {
    return {
      email: values.email,
      password: values.password,
      firstName: values.firstName,
      lastName: values.lastName,
      hireDate: values.hireDate,
      annualSalary: Number(values.annualSalary),
      employmentType: values.employmentType,
      ssnLast4: values.ssnLast4,
    };
  }

  private getReturnUrl(): string {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl?.startsWith('/') && !returnUrl.startsWith('//')) {
      return returnUrl;
    }

    return this.auth.sessionSnapshot?.role === 'Admin' ? '/admin' : '/dashboard';
  }
}
