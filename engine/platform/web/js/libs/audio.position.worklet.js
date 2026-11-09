/**************************************************************************/
/*  orblit.audio.position.worklet.js                                                     */

class OrblitPositionReportingProcessor extends AudioWorkletProcessor {
	static get parameterDescriptors() {
		return [
			{
				name: 'id',
				defaultValue: 0,
				minValue: 0,
				maxValue: 16777215,
				automationRate: 'k-rate',
			},
			{
				name: 'scale',
				defaultValue: 1,
				minValue: 0,
				maxValue: 4,
				automationRate: 'k-rate',
			},
		];
	}

	process(inputs, _outputs, parameters) {
		if (inputs.length > 0) {
			const input = inputs[0];
			if (input.length > 0) {
				const size = input[0].length * parameters['scale'][0];
				this.port.postMessage([parameters['id'][0], size]);
			}
		}
		return true;
	}
}

registerProcessor('orblit-position-reporting-processor', OrblitPositionReportingProcessor);
