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

/** Returns why the API would reject the file, or null when it is acceptable. */
export function validateMedia(file: File, allowVideo: boolean): string | null {
  const extension = extensionOf(file.name);
  if (IMAGE_EXTENSIONS.includes(extension)) {
    return file.size <= MAX_IMAGE_BYTES ? null : `${file.name} is larger than 5 MB.`;
  }
  if (allowVideo && VIDEO_EXTENSIONS.includes(extension)) {
    return file.size <= MAX_VIDEO_BYTES ? null : `${file.name} is larger than 100 MB.`;
  }
  return `${file.name} is not a supported ${allowVideo ? 'image or video' : 'image'} format.`;
}

/** Value for an <input type="file"> accept attribute. */
export function acceptFor(allowVideo: boolean): string {
  return (allowVideo ? [...IMAGE_EXTENSIONS, ...VIDEO_EXTENSIONS] : IMAGE_EXTENSIONS).join(',');
}

function extensionOf(name: string): string {
  const dot = name.lastIndexOf('.');
  return dot < 0 ? '' : name.slice(dot).toLowerCase();
}
