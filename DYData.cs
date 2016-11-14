using UnityEngine;
using System.Collections;

namespace DYGrid
{
    /// <summary>
    /// 所有数据项继承DYData, 并且设置数据项对于的Prefab和Prefab宽度高度
    /// </summary>
    public class DYData
    {

        /// <summary>
        /// 当前数据结构对应的渲染prefab路径
        /// </summary>
        public string prefabPath = "";

        /// <summary>
        /// 用来存储本数据结构对应的渲染prefab的大小
        /// </summary>
        public Vector2 dyDataGridSize = Vector2.zero;



        /// ------------ 以下是运行时DataGrid使用的，无需要设置 ---------------

        /// <summary>
        /// DYDataGrid在执行运行时用来动态保存当前数据位置
        /// </summary>
        public Vector3 itemPos = Vector3.zero;

        /// <summary>
        /// 是否需要渲染
        /// </summary>
        public bool needRender = false;

        /// <summary>
        /// 是否已经渲染出来
        /// </summary>
        public bool isRendered = false;



    }
}
