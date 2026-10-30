const VIDEO_EXTENSIONS = /\.(mp4|webm|mov|avi|mkv)(\?.*)?$/i;

/** The API serves images and videos from the same fields; the file extension tells them apart. */
export function isVideoUrl(url: string | null | undefined): boolean {
  return !!url && VIDEO_EXTENSIONS.test(url);
}
