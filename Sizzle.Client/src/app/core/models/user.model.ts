export interface User {
  id: string;
  name: string;
  email: string;
  imageUrl: string | null;
}

export interface LoginResult {
  userId: string;
  emailConfirmed: boolean;
  isNewUser: boolean;
  jwtToken: string | null;
}
