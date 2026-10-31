import { TranslationKey } from '../i18n/translations';

// Mirrors the API's FileService rules so users get feedback before uploading.
export const IMAGE_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp', '.gif'];
export const VIDEO_EXTENSIONS = ['.mp4', '.webm', '.mov', '.avi', '.mkv'];
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
export const MAX_VIDEO_BYTES = 100 * 1024 * 1024;

const VIDEO_URL = new RegExp(`(${VIDEO_EXTENSIONS.map((e) => `\\${e}`).join('|')})(\\?.*)?$`, 'i');

/** The API serves images and videos from the same fields; the file extension tells them apart. */
export function isVideoUrl(url: string | null | undefined): boolean {
  return !!url && VIDEO_URL.test(url);
}

export function isVideoFile(file: File): boolean {
  return VIDEO_EXTENSIONS.includes(extensionOf(file.name));
}

export interface MediaProblem {
  key: TranslationKey;
  params: { file: string };
}

/** Returns why the API would reject the file (as a message key), or null when it is acceptable. */
export function validateMedia(file: File, allowVideo: boolean): MediaProblem | null {
  const extension = extensionOf(file.name);
  const problem = (key: TranslationKey): MediaProblem => ({ key, params: { file: file.name } });
  if (IMAGE_EXTENSIONS.includes(extension)) {
    return file.size <= MAX_IMAGE_BYTES ? null : problem('media.imageTooLarge');
  }
  if (allowVideo && VIDEO_EXTENSIONS.includes(extension)) {
    return file.size <= MAX_VIDEO_BYTES ? null : problem('media.videoTooLarge');
  }
  return problem(allowVideo ? 'media.unsupportedMedia' : 'media.unsupportedImage');
}

/** Value for an <input type="file"> accept attribute. */
export function acceptFor(allowVideo: boolean): string {
  return (allowVideo ? [...IMAGE_EXTENSIONS, ...VIDEO_EXTENSIONS] : IMAGE_EXTENSIONS).join(',');
}

function extensionOf(name: string): string {
  const dot = name.lastIndexOf('.');
  return dot < 0 ? '' : name.slice(dot).toLowerCase();
}
