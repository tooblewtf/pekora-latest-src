import Link from "../link";
import getFlag from "../../lib/getFlag";

const ForumHeader = props => {
  return <div className='row'>
    <div className='col-6'>
      {props.children}
    </div>
    <div className='col-6'>
      <p className='text-end fw-bold'>
        <span>
          <Link href='/Forum/Default.aspx'>
            <a className='normal'>Home</a>
          </Link>
        </span>
        {getFlag('forumSearchEnabled', false) ? <span className='pe-1 ps-1'>|</span> : null}
        {getFlag('forumSearchEnabled', false) ? <span>
          <Link href='/Forum/Search.aspx'>
            <a className='normal'>Search</a>
          </Link>
        </span> : null}
        <span className='pe-1 ps-1'>|</span>
        <span>
          <Link href='/Forum/MyForums.aspx'>
            <a className='normal'>MyForums</a>
          </Link>
        </span>
      </p>
    </div>
  </div>
}

export default ForumHeader;

export const ForumHeaderSubCategory = ({cat, sub}) => {
  return <ForumHeader>
    <p className='fw-bold'>
      <Link href='/Forum/Default.aspx'><a>Marine Forum</a></Link>{' » '}
      <Link href={'/Forum/ForumGroup.aspx?ForumID='+cat.id}><a>{cat.name}</a></Link>{' » '}
      <Link href={'/Forum/ShowForum.aspx?ForumID=' + sub.id}><a>{sub.name}</a></Link>
    </p>
  </ForumHeader>
}