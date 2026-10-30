export interface Comment {
  id: string;
  parentId: string | null;
  content: string;
  createdAt: string;
  userId: string;
  userName: string;
  userImageUrl: string | null;
  recipeId: string;
}
