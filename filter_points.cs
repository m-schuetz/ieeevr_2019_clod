#version 450

// author: Markus Schütz
// license: MIT license (https://opensource.org/licenses/MIT)

// Source for paper: "Real-Time Continuous Level of Detail Rendering of Point Clouds"
// Markus Schütz, Katharina Krösl, Michael Wimmer
// IEEE VR 2019, March, Osaka
//	
// This compute shader is executed for each point of the 
// full point cloud (inputBuffer) and it stores a selected subset 
// with continuous LOD properties in a new vertex buffer (targetBuffer).
// 
// - This is an in-core method
// - It downsamples ~86M points to 5M points in ~5.45ms on a GTX 1080 => 15.9M points / ms.
// - Initial tests for an RTX 2080 TI have shown performances up to 44M points / ms.
// - Each input point needs a level attribute in the alpha channel of the color
// - In VR, this method is distributed over multiple frames, 
//   e.g. process 18M points per frame of the input buffer,
//   which takes roughly 1.1ms per frame. 
//   After 5 frames, the new downsampled vertex buffer is finished
//   and it will be used to render the point cloud for the next 5 frames.
// - Points are culled against an "extented-frustum" so that enough points are available 
//   during motion even though the rendered model is computed for the frustum from 5 frames earlier.
// - Distribution of the downsampling step over multiple frames is actually not necessary anymore for the 2080 TI.
//   The same models with the same LOD can be downsampled and rendered at 90FPS in a single frame on a 2080 TI, 
//   compared to a 1080 that required distribution of the downsampling step over ~5 frames.
//
	
layout(local_size_x = 128, local_size_y = 1) in;

struct Vertex{
	float x;
	float y;
	float z;
	uint colors;
};

layout(std430, binding = 0) buffer ssInputBuffer{
	Vertex inputBuffer[];
};

layout(std430, binding = 1) buffer ssTargetBuffer{
	Vertex targetBuffer[];
};

layout(std430, binding = 3) buffer ssDrawParameters{
	uint  count;
	uint  primCount;
	uint  first;
	uint  baseInstance;
} drawParameters;

layout(location = 21) uniform int uBatchOffset;
layout(location = 22) uniform int uBatchSize;

layout(std140, binding = 4) uniform shader_data{
	mat4 transform;
	mat4 world;
	mat4 view;
	mat4 proj;

	vec2 screenSize;
	vec4 pivot;

	float CLOD;
	float scale;
	float spacing;
	float time;
} ssArgs;


float rand(float n){
	return fract(cos(n) * 123456.789);
}

void main(){

	uint inputIndex = gl_GlobalInvocationID.x;

	if(inputIndex > uBatchSize){
		return;
	}

	inputIndex = inputIndex + uBatchOffset;

	Vertex v = inputBuffer[inputIndex];

	vec3 aPosition = vec3(v.x, v.y, v.z);
	float level = float((v.colors & 0xFF000000) >> 24);
	float aRandom = rand(v.x + v.y + v.z);

	vec4 projected = (ssArgs.transform * vec4(aPosition, 1));
	projected.xyz = projected.xyz / projected.w;

	// extented-frustum culling
	float extent = 2;
	if(abs(projected.x) > extent || abs(projected.y) > extent){
		return;
	}

	// near-clipping
	if(projected.w < 0){
		return;
	}

	vec3 worldPos = (ssArgs.world * vec4(aPosition, 1)).xyz;

	// without level randomization
	//float pointSpacing = uScale * uSpacing / pow(2, level);

	// with level randomization
	float pointSpacing = ssArgs.scale * ssArgs.spacing / pow(2, level + aRandom);

	float d = distance(worldPos, ssArgs.pivot.xyz);
	float dc = length(projected.xy);

	// targetSpacing dependant on camera distance
	//float targetSpacing = (ssArgs.CLOD / 1000) * d;

	// dependant on cam distance and distance to center of screen
	float targetSpacing = (d * ssArgs.CLOD) / (1000 * max(1 - 0.7 * dc , 0.3));

	// reduce density away from center with the gaussian function
	// no significant improvement over 1 / (d - dc), so we've settled with the simpler one
	//float sigma = 0.4;
	//float gbc = (1 / (sigma * sqrt(2 * 3.1415))) * exp(-0.5 * pow( dc / sigma, 2.0 ));
	//targetSpacing = (1. * d * ssArgs.CLOD) / (1000 * gbc);

	if(pointSpacing < targetSpacing){
		return;
	}

	int targetIndex = int(atomicAdd(drawParameters.count, 1));
	targetBuffer[targetIndex] = v;
}


