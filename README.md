# ieeevr_2019_clod

Source for paper: "Real-Time Continuous Level of Detail Rendering of Point Clouds",
Markus Schütz, Katharina Krösl, Michael Wimmer,
IEEE VR 2019, March, Osaka

The compute shader ```filter_points.cs``` is executed for each point of the 
full point cloud (inputBuffer) and it stores a selected subset 
with continuous LOD properties in a new vertex buffer (targetBuffer).

```pointcloud_clod.vs``` then renders the downsampled vertex buffer and also computes point sizes based on the sampling density / target spacing.

- This is an in-core method
- It downsamples ~86M points to 5M points in ~5.45ms on a GTX 1080 => 15.9M points / ms.
- Initial tests for an RTX 2080 TI have shown performances of roughly ~86M points to 3M points in ~2ms => 43M points / ms. For reference, a frame in VR has to be computed in around 11ms.
- Each input point needs a level attribute in the alpha channel of the color
- In VR, this method is distributed over multiple frames, 
  e.g. process 18M points per frame of the input buffer,
  which takes roughly 1.1ms per frame. 
  After 5 frames, the new downsampled vertex buffer is finished
  and it will be used to render the point cloud for the next 5 frames.
- Points are culled against an "extented-frustum" so that enough points are available 
  during motion even though the rendered model is computed for the frustum from 5 frames earlier.
- Distribution of the downsampling step over multiple frames is actually not necessary anymore for the 2080 TI.
  The same models with the same LOD can be downsampled and rendered at 90FPS in a single frame on a 2080 TI, 
  compared to a 1080 that required distribution of the downsampling step over ~5 frames.
