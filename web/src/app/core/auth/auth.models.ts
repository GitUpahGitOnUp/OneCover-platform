export interface AuthResponse {
  token: string;
  role: string;
}

export interface AuthSession {
  token: string;
  role: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegistrationRequest {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
  hireDate: string;
  annualSalary: number;
  employmentType: 'FullTime' | 'PartTime';
  ssnLast4: string;
}

export interface MemberProfile {
  id: number;
  email: string;
  role: string;
  firstName: string;
  lastName: string;
  hireDate: string;
  annualSalary: number;
  employmentType: 'FullTime' | 'PartTime';
  ssnLast4: string;
  isFmlaEligible: boolean;
}
