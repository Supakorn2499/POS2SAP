export interface LoginRequest {
  staffLogin: string;
  staffPassword: string;
}

export interface LoginResultDto {
  staffLogin: string;
  staffFirstName: string;
  staffLastName: string;
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}
