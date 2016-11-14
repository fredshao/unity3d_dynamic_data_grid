using UnityEngine;
using System.Collections;

namespace DYGrid
{
    /// <summary>
    /// 所有的Item Prfab控制脚本继承 DYDataGridItem， 自己实现 Render 和 Release方法
    /// </summary>
    public class DYDataGridItem : MonoBehaviour
    {

        private RectTransform _rectTrans = null;

        public RectTransform rectTrans
        {
            get
            {
                if(this._rectTrans == null)
                {
                    this._rectTrans = this.gameObject.GetComponent<RectTransform>();
                }
                return this._rectTrans;
            }
        }

        private DYData _data;

        public DYData data
        {
            get
            {
                return this._data;
            }
            set
            {
                this._data = value;
            }
        }



        /// <summary>
        /// 渲染一个数据结构项
        /// </summary>
        public virtual void Render()
        {
            
        }


        /// <summary>
        /// 回收一个Prefab,需要将控制脚本运行时动态创建的资源销毁，例如从网上下载的图片等，
        /// </summary>
        public virtual void Release()
        {

        }


    }
}
