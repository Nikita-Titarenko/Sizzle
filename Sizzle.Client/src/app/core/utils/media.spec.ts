import { MAX_IMAGE_BYTES, MAX_VIDEO_BYTES, isVideoUrl, validateMedia } from './media';

/** File with a fake size, so tests do not allocate megabytes. */
function file(name: string, size = 1024): File {
  const f = new File(['x'], name);
  Object.defineProperty(f, 'size', { value: size });
  return f;
}

// These limits mirror the API's FileService; if they drift, uploads fail only on the server.
describe('validateMedia', () => {
  it('accepts supported images up to 5 MB', () => {
    expect(validateMedia(file('a.JPG', MAX_IMAGE_BYTES), false)).toBeNull();
    expect(validateMedia(file('a.webp'), false)).toBeNull();
  });

  it('rejects images over 5 MB', () => {
    expect(validateMedia(file('a.png', MAX_IMAGE_BYTES + 1), false)?.key).toBe(
      'media.imageTooLarge',
    );
  });

  it('accepts videos only where video is allowed', () => {
    expect(validateMedia(file('clip.mp4'), true)).toBeNull();
    expect(validateMedia(file('clip.mp4'), false)?.key).toBe('media.unsupportedImage');
  });

  it('rejects videos over 100 MB', () => {
    expect(validateMedia(file('clip.mov', MAX_VIDEO_BYTES + 1), true)?.key).toBe(
      'media.videoTooLarge',
    );
  });

  it('rejects other formats and names the file', () => {
    expect(validateMedia(file('notes.pdf'), true)).toEqual({
      key: 'media.unsupportedMedia',
      params: { file: 'notes.pdf' },
    });
  });
});

describe('isVideoUrl', () => {
  it.each([
    ['http://localhost/uploads/a.mp4', true],
    ['/uploads/a.WEBM?v=2', true],
    ['/uploads/a.jpg', false],
    [null, false],
  ])('%s → %s', (url, expected) => {
    expect(isVideoUrl(url)).toBe(expected);
  });
});
