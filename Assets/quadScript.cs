using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using Dicom;
using UnityEditor.Search;
using UnityEngine.Rendering;

public class quadScript : MonoBehaviour {

    // Dicom har et "levende" dictionary som leses fra xml ved initDicom
    // slices må sorteres, og det basert på en tag, men at pixeldata lesing er en separat operasjon, derfor har vi nullpeker til pixeldata
    // dicomfile lagres slik at fil ikke må leses enda en gang når pixeldata hentes
    
    // member variables of quadScript, accessible from any function
    Slice[] _slices;
    int _numSlices;
    int _minIntensity;
    int _maxIntensity;


    private Button _button;
    private Toggle _toggle;
    private Slider _slider1;
    private Slider _slider2;

    private Texture2D _texture;


    private int _circleRadius = 50;
    private int _z = 50;
    private int _dimensions = 100;
    

    private int xdim = 100;
    private int ydim = 100;
    private int zdim = 70;

    private float[,,] volumeData;
    
    private int _slider1Val = 84;
    private float _iso = 0.3f;
    
    private List<Vector3> vertices;
    private List<int> indices;
    private meshScript mscript;


    // Use this for initialization
    void Start ()
    {
        
        var watch = System.Diagnostics.Stopwatch.StartNew();
        
         var uiDocument = GameObject.Find("MyUIDocument").GetComponent<UIDocument>();
        _button = uiDocument.rootVisualElement.Q("button1") as Button;
        _toggle = uiDocument.rootVisualElement.Q("toggle") as Toggle;
        _slider1 = uiDocument.rootVisualElement.Q("slider1") as Slider;
        _slider2 = uiDocument.rootVisualElement.Q("slider2") as Slider;
        _button.RegisterCallback<ClickEvent>(button1Pushed);
        _slider1.RegisterValueChangedCallback(slicePosSliderChange);
        _slider2.RegisterValueChangedCallback(sliceIsoSliderChange);

        Slice.initDicom();

        string dicomfilepath = Application.dataPath + @"\..\dicomdata\"; // Application.dataPath is in the assets folder, but these files are "managed", so we go one level up
   
        _slices = processSlices(dicomfilepath); // loads slices from the folder above
        CreateVolumeData();
        setTexture(_slices[0]);                     // shows the first slice
        
        // generateIsoline(_slices[0], 0.0f);

        //  gets the mesh object and uses it to create a diagonal line
        
        
        
        mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        // List<Vector3> vertices = new List<Vector3>();
        // List<int> indices = new List<int>();
        // vertices.Add(new Vector3(-0.5f,-0.5f,0));
        // vertices.Add(new Vector3(-0.5f,0.5f,0));
        // vertices.Add(new Vector3(0.5f,0.5f,0));
        // vertices.Add(new Vector3(0.5f,-0.5f,0));
        // indices.Add(0);
        // indices.Add(2);
        // indices.Add(1);
        // indices.Add(3);
        
        // int xdim = _slices[0].sliceInfo.Rows;
        // int ydim = _slices[0].sliceInfo.Columns;
        // marchingSquares(xdim, ydim);
        // mscript.createMeshGeometry(vertices, indices);
        // marchingTetrahedra(_dimensions, _dimensions, _dimensions, _slices[0]);
        marchingTetrahedra();
        
        watch.Stop();
        var elapsedTime = watch.ElapsedMilliseconds;
        print("Time to perform Marching Tetrahedron: " + elapsedTime / 1000 + "s");

    }
    


    Slice[] processSlices(string dicomfilepath)
    {
        string[] dicomfilenames = Directory.GetFiles(dicomfilepath, "*.IMA"); 
        _numSlices =  dicomfilenames.Length;

        Slice[] slices = new Slice[_numSlices];

        float max = -1;
        float min = 99999;
        for (int i = 0; i < _numSlices; i++)
        {
            string filename = dicomfilenames[i];
            slices[i] = new Slice(filename);
            SliceInfo info = slices[i].sliceInfo;
            if (info.LargestImagePixelValue > max) max = info.LargestImagePixelValue;
            if (info.SmallestImagePixelValue < min) min = info.SmallestImagePixelValue;
            // Del dataen på max før den settes inn i tekstur
            // alternativet er å dele på 2^dicombitdepth,  men det ville blitt 4096 i dette tilfelle

        }
        print("Number of slices read:" + _numSlices);
        print("Max intensity in all slices:" + max);
        print("Min intensity in all slices:" + min);

        _minIntensity = (int)min;
        _maxIntensity = (int)max;
        //_iso = 0;

        Array.Sort(slices);
        
        return slices;
    }

    void CreateVolumeData()
    {
        int rows = _slices[0].sliceInfo.Rows;
        int cols = _slices[0].sliceInfo.Columns;
        int numSlices = _slices.Length;
        volumeData = new float[cols, rows, numSlices];

        for (int z = 0; z < numSlices; z++)
        {
            ushort[] pixels = _slices[z].getPixels();
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    volumeData[x, y, z] = (pixels[x + y * cols] - _minIntensity) / (float)(_maxIntensity - _minIntensity);
                }
            }
        }
    }
    
    int getPointData(Vector3 point)
    {
        //DICOM DATA BOUNDS: X & Y = 512, Z = 354
        if (point.x < 0 || point.x > 511 || point.y < 0 || point.y > 511 || point.z < 0 || point.z > 353)  
        {
            return 0;
        }
        return _slices[(int)point.z].getPixels()[(int)point.x + ( (int)point.y * 512)];
    }
    
    float GetVolumeValue(int x, int y, int z)
    {
        // Get the dimensions of the volumeData array
        int maxX = volumeData.GetLength(0);
        int maxY = volumeData.GetLength(1);
        int maxZ = volumeData.GetLength(2);

        // If the requested coordinate is out-of-bounds, return 0
        if (x < 0 || x >= maxX || y < 0 || y >= maxY || z < 0 || z >= maxZ)
        {
            return 0.0f;
        }

        return volumeData[x, y, z];
    }
    

    void setTexture(Slice slice)
    {
        // int xdim = slice.sliceInfo.Rows;
        // int ydim = slice.sliceInfo.Columns;
        int centerx = xdim / 2;
        int centery = ydim / 2;
        int centerz = centerx;

        var texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false);     // garbage collector will tackle that it is new'ed 

        ushort[] pixels = slice.getPixels();
        
        
        for (int y = 0; y < ydim; y++)
            for (int x = 0; x < xdim; x++)
            {

                float val = pixelval(new Vector2(x, y), xdim, pixels);
                float v = (val - _minIntensity) / (float)(_maxIntensity - _minIntensity) - 0.5f; // maps [_minIntensity,_maxIntensity] to [0,1] , i.e.  _minIntensity to black and _maxIntensity to white
                // if (Math.Sqrt(Math.Pow((centerx-x),2)+Math.Pow((centery-y),2))<xdim)
                if (distanceToOrigo2D(centerx, x, centery, y) < xdim)
                // if (getNormalizedValue(x, y, centerx, centery, xdim) < _slider1Val)
                {
                    // texture.SetPixel(x, y, new UnityEngine.Color(1, 1, 1));
                    // v = 1;
                    // v = (float)(Math.Sqrt(Math.Pow((centerx - x), 2) + Math.Pow((centery - y), 2)) / xdim);
                    v = (float)distanceToOrigo2D(centerx, x, centery, y) / xdim;
                    // v = getNormalizedValue(x, y, centerx, centery, xdim);
                }
                else
                {
                    // texture.SetPixel(x, y, new UnityEngine.Color(0, 0, 0));
                    v = 1;
                }
                texture.SetPixel(x, y, new UnityEngine.Color(v, v, v));
            }
        
        texture.filterMode = FilterMode.Point;  // nearest neighbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply();  // Apply all SetPixel calls
        _texture = texture;
        GetComponent<Renderer>().material.mainTexture = _texture;
    }
    
        void setTexture3D(Slice slice)
    {
        // int xdim = slice.sliceInfo.Rows;
        // int ydim = slice.sliceInfo.Columns;
        int xdim = _dimensions;
        int ydim = _dimensions;
        int centerx = xdim / 2;
        int centery = ydim / 2;
        int centerz = centerx;

        var texture = new Texture2D(xdim, ydim, TextureFormat.RGB24, false);     // garbage collector will tackle that it is new'ed 

        ushort[] pixels = slice.getPixels();
        
        
        for (int y = 0; y < ydim; y++)
            for (int x = 0; x < xdim; x++)
            {
                float distance = distanceToOrigo3D(x, centerx, y, centery, _z, centerz);
                float normalizedDist = Mathf.Clamp01(distance / _circleRadius);
                float v = normalizedDist;

                Color pixelColor = new Color(v, v, v, 1);
                texture.SetPixel(x, y, pixelColor);
            }
        
        texture.filterMode = FilterMode.Point;  // nearest neighbor interpolation is used.  (alternative is FilterMode.Bilinear)
        texture.Apply();  // Apply all SetPixel calls
        _texture = texture;
        GetComponent<Renderer>().material.mainTexture = _texture;
    }
    
    void marchingSquares(int xdim, int ydim, Slice slice){
        
        for (int y = 0; y < ydim - 1; y++){
            for (int x = 0; x < xdim - 1; x++){
                
                Vector3 p1 = PixelToNormalized(x, y, xdim, ydim);
                Vector3 p2 = PixelToNormalized(x + 1, y, xdim, ydim);
                Vector3 p3 = PixelToNormalized(x + 1, y + 1, xdim, ydim);
                Vector3 p4 = PixelToNormalized(x, y + 1, xdim, ydim);
                
                float bottomLeft = getTextureIntensity(x, y, xdim, ydim);
                float bottomRight = getTextureIntensity(x + 1, y, xdim, ydim);
                float topRight = getTextureIntensity(x + 1, y + 1,  xdim, ydim);
                float topLeft = getTextureIntensity(x, y + 1, xdim, ydim);
                
                // float bottomLeft = getPixelIntensity(x, y, xdim, pixels);
                // float bottomRight = getPixelIntensity(x + 1, y, xdim, pixels);
                // float topRight = getPixelIntensity(x + 1, y + 1,  xdim, pixels);
                // float topLeft = getPixelIntensity(x, y + 1, xdim, pixels);
                
                Vector3 iBottom = interpolate(p1, p2, bottomLeft, bottomRight);
                Vector3 iRight = interpolate(p2, p3, bottomRight, topRight);
                Vector3 iTop = interpolate(p4, p3, topLeft, topRight);
                Vector3 iLeft = interpolate(p1, p4, bottomLeft, topLeft);


                int celltype = 0;
                if (bottomLeft >= _iso) celltype |= 1;
                if (bottomRight >= _iso) celltype |= 2;
                if (topRight >= _iso) celltype |= 4;
                if (topLeft >= _iso) celltype |= 8;
                

                switch (celltype)
                {
                    case 0:
                    case 15:
                        break;
                    case 1:
                        // bottom -> left
                        drawLine(iBottom, iLeft);
                        break;
                    case 14:
                        drawLine(iLeft, iBottom);
                        break;
                    case 2:
                        // right -> bottom
                        drawLine(iRight, iBottom);
                        break;
                    case 13:
                        drawLine(iBottom, iRight);
                        break;
                    case 3:
                        // right -> left
                        drawLine(iRight, iLeft);
                        break;
                    case 12:
                        drawLine(iLeft, iRight);
                        break;
                    case 4:
                        // right -> top
                        drawLine(iRight, iTop);
                        break;
                    case 11:
                        drawLine(iTop, iRight);
                        break;
                    // ambiguous case
                    case 5:
                        drawLine(iBottom, iLeft);
                        drawLine(iTop, iRight);
                        break;
                    case 6:
                        // bottom -> top
                        drawLine(iBottom, iTop);
                        break;
                    case 9:
                        drawLine(iTop, iBottom);
                        break;
                    case 7:
                        // left -> top
                        drawLine(iLeft, iTop);
                        break;
                    case 8:
                        drawLine(iTop, iLeft);
                        break;
                    // ambiguous case
                    case 10:
                        drawLine(iRight, iBottom);
                        drawLine(iLeft, iTop);
                        break;
                }
            }
        }
    }
    
    void marchingTetrahedra()
    {
        mscript = GameObject.Find("GameObjectMesh").GetComponent<meshScript>();
        vertices = new List<Vector3>();
        indices = new List<int>();
        // for (int x = 0; x < xdim; x++)
        for (int x = -1; x < volumeData.GetLength(0) + 1; x++)
        {
            // for (int y = 0; y < ydim; y++)
            for (int y = -1; y < volumeData.GetLength(1) + 1; y++)
            {
                // for (int z = 0; z < zdim; z++)
                for (int z = -1; z < volumeData.GetLength(2) + 1; z++)
                {
                    doCube(x, y, z);
                }
            }
        }
        // mscript.createMeshGeometry(vertices, indices);
        string filNavn = "mesh.obj";
        // mscript.MeshToFile(filNavn);
        mscript.MeshToFile1(filNavn, ref vertices, ref indices);
    }
    
    float getIsoCircle(int x, int y, int z)
    {
        int xmid=xdim/2;
        int ymid=ydim/2;
        int zmid=zdim/2;
        float distance=Mathf.Sqrt(Mathf.Pow(x-xmid,2) + Mathf.Pow(y-ymid,2) + Mathf.Pow(z-zmid,2));
        float v = Mathf.Clamp01(distance/(xdim/2));
        return v;
    }
    
    void doCube(int x, int y, int z)
    {
        int dim = xdim;
        Vector3 offset = new Vector3(-0.5f,-0.5f,-0.5f);
        
        Vector3 v0 = new Vector3(x, y, z) / dim + offset;
        Vector3 v1 = new Vector3(x+1, y, z) / dim + offset;
        Vector3 v2 = new Vector3(x, y+1, z) / dim + offset;
        Vector3 v3 = new Vector3(x+1, y+1, z) / dim + offset;
        
        Vector3 v4 = new Vector3(x, y, z+1) / dim + offset;
        Vector3 v5 = new Vector3(x+1, y, z+1) / dim + offset;
        Vector3 v6 = new Vector3(x, y+1, z+1) / dim + offset;
        Vector3 v7 = new Vector3(x + 1, y + 1, z + 1) / dim + offset;
        
        float p0 = GetVolumeValue(x,y,z);
        float p1 = GetVolumeValue(x+1,y,z);
        float p2 = GetVolumeValue(x,y+1,z);
        float p3 = GetVolumeValue(x+1,y+1,z);
        
        float p4 = GetVolumeValue(x,y,z+1);
        float p5 = GetVolumeValue(x+1,y,z+1);
        float p6 = GetVolumeValue(x,y+1,z+1);
        float p7 = GetVolumeValue(x+1,y+1,z+1);
        
        
        
        doTetra(v4,v6,v0,v7, p4,p6,p0,p7);
        doTetra(v6,v0,v7,v2, p6,p0,p7,p2);
        doTetra(v0,v7,v2,v3, p0,p7,p2,p3);
        doTetra(v4,v5,v7,v0, p4,p5,p7,p0);
        doTetra(v1,v7,v0,v3, p1,p7,p0,p3);
        doTetra(v0,v5,v7,v1, p0,p5,p7,p1);
    }
    
    void doTetra (Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
                 float p0,  float p1,  float p2,  float p3)
    {
        
        int i1 = (_iso>p0)? 1 : 0;
        int i2 = (_iso>p1)? 1 : 0;
        int i3 = (_iso>p2)? 1 : 0;
        int i4 = (_iso>p3)? 1 : 0;
        int celltype = 8*i1 + 4*i2 + 2*i3 + 1*i4;
        
        Vector3 p12 = Interpolate(v0,v1, p0, p1, _iso);
        Vector3 p13 = Interpolate(v0,v2, p0, p2, _iso);
        Vector3 p14 = Interpolate(v0,v3, p0, p3, _iso);
        Vector3 p23 = Interpolate(v1,v2, p1, p2, _iso);
        Vector3 p24 = Interpolate(v1,v3, p1, p3, _iso);
        Vector3 p34 = Interpolate(v2,v3, p2, p3, _iso);
        
        
        switch(celltype){
            case 0:
                break;  
            case 1 or 14:
                AddTriangle(p14, p34, p24);
                break;
            case 2 or 13:
                AddTriangle(p13, p23, p34);
                break;  
            case 3 or 12:
                AddQuad(p13 ,p23 ,p24 ,p14);
                break;  
            case 4 or 11:
                AddTriangle(p12, p24, p23);
                break;  
            case 5 or 10: 
                AddQuad(p12, p23, p34,p14);
                break;
            case 6 or 9:
                AddQuad(p12, p13, p34,p24);
                break;
            case 7 or 8:
                AddTriangle(p12,p13,p14);
                break;
            case 15:
                break;
        }
    }

    int getState(int a, int b, int c, int d)
    {
        return a * 8 + b * 4 + c * 2 + d * 1;
    }

    void drawLine(Vector3 v1, Vector3 v2)
    {
        AddLineSegment(v1.x, v1.y, v2.x, v2.y);
    }
    
    void AddLineSegment(float x1, float y1, float x2, float y2) {
        int index = vertices.Count;
        vertices.Add(new Vector3(x1, y1, 0));
        vertices.Add(new Vector3(x2, y2, 0));
        indices.Add(index);
        indices.Add(index + 1);
    }
    
    void AddTriangle(Vector3 a, Vector3 b, Vector3 c) {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        indices.Add(index);
        indices.Add(index + 1);
        indices.Add(index + 2);
        indices.Add(index + 2);
        indices.Add(index + 1);
        indices.Add(index);
    }
    
    void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, d, c);
        // AddTriangle(a, c, d);
    }
    
    Vector3 Interpolate(Vector3 p1, Vector3 p2, float val1, float val2, float iso) {
        if (Mathf.Abs(val2 - val1) < 0.00001f)
            return 0.5f * (p1 + p2);

        float t = (_iso - val1) / (val2 - val1);
        return p1 + t * (p2 - p1);
    }
    
    Vector3 interpolate(Vector3 point1, Vector3 point2, float value1, float value2) {
        if (Mathf.Abs(value1 - value2) < 0.0001f) return (point1 + point2) * 0.5f; // avoid division by zero, returns midpoint between the two points
        float t = (_iso - value1) / (value2 - value1); // compute relative position of isoValue
        return point1 * (1 - t) + point2 * t; // return weighted interpolation
    }

    float distanceToOrigo3D(int px, int x, int py, int y, int pz, int z)
    {
        return (Mathf.Sqrt(Mathf.Pow((px - x), 2) + Mathf.Pow((py - y), 2)) + Mathf.Pow((pz - z), 2));
    }
    
    double distanceToOrigo2D(int px, int x, int py, int y)
    {
        return (Math.Sqrt(Math.Pow((px - x), 2) + Math.Pow((py - y), 2)));
    }
    
    float getNormalizedValue(float x, float y, int centerx, int centery, int xdim) {
        float d = Mathf.Sqrt((centerx - x) * (centerx - x) + (centery - y) * (centery - y));
        if (d < xdim)
            return d / xdim;
        return 0;
    }
    
    Vector3 PixelToNormalized(int x, int y, int xdim, int ydim)
    {
        float nx = (float)x / xdim - 0.5f;
        float ny = (float)y / ydim - 0.5f;
        return new Vector3(nx, ny, 0);
    }
    
    Vector3 PixelToNormalizedTetra(int x, int y, int z, int xdim, int ydim, int zdim)
    {
        float nx = (float)x / xdim - 0.5f;
        float ny = (float)y / ydim - 0.5f;
        float nz = (float)z / zdim - 0.5f;
        return new Vector3(nx, ny, nz);
    }
    
    float getPixelIntensity(int x, int y, int xdim, ushort[] pixels) {
        // Get the raw pixel value
        ushort rawVal = pixels[x + y * xdim];
        // Normalize it to a value between 0 and 1 using the stored _minIntensity and _maxIntensity
        return (rawVal - _minIntensity) / (float)(_maxIntensity - _minIntensity);
    }

    float getTextureIntensity(int x, int y, int xdim, int ydim)
    {
        x = Mathf.Clamp(x, 0, xdim - 1);
        y = Mathf.Clamp(y, 0, ydim - 1);
        Color c = _texture.GetPixel(x, y);
        return c.grayscale;
    }
    
    float getTextureIntensityTetra(int x, int y, int z, int xdim, int ydim, int zdim)
    {
        x = Mathf.Clamp(x, 0, xdim - 1);
        y = Mathf.Clamp(y, 0, ydim - 1);
        z = Mathf.Clamp(z, 0, zdim - 1);
        Color c = _texture.GetPixel(x, y);
        return c.grayscale;
    }

    
    ushort pixelval(Vector2 p, int xdim, ushort[] pixels)
    {
        return pixels[(int)p.x + (int)p.y * xdim];
    }
    
    Vector2 vec2(float x, float y)
    {
        return new Vector2(x, y);
    }


    // Update is called once per frame
    void Update () {
        
      
    }
       
    public void slicePosSliderChange(ChangeEvent<float> evt)
    {
        print("slicePosSliderChange:" + evt.newValue);
        vertices.Clear();
        indices.Clear();
        setTexture(_slices[0]);
        _slider1Val=(int)evt.newValue;
        _circleRadius = _slider1Val;
        setTexture3D(_slices[0]);
        // marchingSquares(_dimensions,_dimensions, _slices[0]);
        // marchingTetrahedra(xdim, ydim, zdim, volumeData);
        marchingTetrahedra();
        mscript.createMeshGeometry(vertices, indices);
    }
   
    public void sliceIsoSliderChange(ChangeEvent<float> evt)
    {
        _iso = evt.newValue;
        print("sliceIsoSliderChange:" + _iso);
    }
    
    public void button1Pushed(ClickEvent evt)
    {
        vertices.Clear();
        indices.Clear();
        // int xdim = _slices[0].sliceInfo.Rows;
        // int ydim = _slices[0].sliceInfo.Columns;
        // marchingSquares(xdim, ydim, _slices[0]);
        // marchingSquares(_dimensions, _dimensions, _slices[0]);
        // marchingTetrahedra(xdim, ydim, zdim, volumeData);
        marchingTetrahedra();
        mscript.createMeshGeometry(vertices, indices);
        print("button1Pushed"); 
    }

    public void button2Pushed()
    {
        print("button2Pushed"); 
    }

}
