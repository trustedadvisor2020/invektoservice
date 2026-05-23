// AudioWorkletProcessor: captures 128-sample chunks from getUserMedia,
// accumulates to 960-sample frames (20ms @ 48kHz mono), encodes as PCM16 LE,
// and posts ArrayBuffer to the main thread for WS transmission.

class PcmFrameProcessor extends AudioWorkletProcessor {
  constructor(options) {
    super();
    this.frameSamples = (options && options.processorOptions && options.processorOptions.frameSamples) || 960;
    this.buffer = new Float32Array(this.frameSamples);
    this.bufferOffset = 0;
  }

  process(inputs) {
    const input = inputs[0];
    if (!input || input.length === 0) return true;
    const channel = input[0];  // mono
    if (!channel) return true;

    let inOffset = 0;
    while (inOffset < channel.length) {
      const room = this.frameSamples - this.bufferOffset;
      const take = Math.min(room, channel.length - inOffset);
      this.buffer.set(channel.subarray(inOffset, inOffset + take), this.bufferOffset);
      this.bufferOffset += take;
      inOffset += take;

      if (this.bufferOffset === this.frameSamples) {
        // Convert Float32 [-1,1] → PCM16 LE
        const pcm = new Int16Array(this.frameSamples);
        for (let i = 0; i < this.frameSamples; i++) {
          let s = this.buffer[i];
          if (s < -1) s = -1;
          else if (s > 1) s = 1;
          pcm[i] = s < 0 ? Math.round(s * 32768) : Math.round(s * 32767);
        }
        // Post ArrayBuffer (transferable) to main thread
        this.port.postMessage(pcm.buffer, [pcm.buffer]);
        this.bufferOffset = 0;
      }
    }

    return true; // keep processor alive
  }
}

registerProcessor('pcm-frame-processor', PcmFrameProcessor);
