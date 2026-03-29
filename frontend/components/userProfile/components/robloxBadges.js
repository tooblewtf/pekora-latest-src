import { useEffect, useState } from "react";
import { createUseStyles } from "react-jss"
import { getUserRobloxBadges } from "../../../services/accountInformation";
import SmallTextLink from "./smallTextLink";
import Subtitle from "./subtitle";
import Link from "../../link";

const useBadgeStyles = createUseStyles({
  label: {
    width: '100%',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    fontSize: '16px',
    lineHeight: '1.4em',
    fontWeight: 500,
  },
  imageWrapper: {
    border: '1px solid var(--text-color-secondary)',
    //borderRadius: '4px',
    borderRadius: 0,
    width: '142px',
    height: '142px',
    overflow: 'hidden',
    minWidth: '142px',
  },
  buttonWrapper: {
    //width: '100px',
    float: 'right',
  },
  badgeLink: {
    color: 'inherit',
  },
});
const RobloxBadges = props => {
  const s = useBadgeStyles();
  const [badges, setBadges] = useState(null);
  const [showAll, setShowAll] = useState(false);

  useEffect(() => {
    getUserRobloxBadges({ userId: props.userId }).then(setBadges);
  }, [props.userId]);

  if (!badges || !badges.length) return null;

  return <div className='flex d-none d-lg-flex marginStuff'>
    <div className='col-10'>
      <Subtitle>Marine Badges ({badges?.length || 0})</Subtitle>
    </div>
    <div className='col-6 col-lg-2'>
      {badges && badges.length > 6 &&
        <div className={s.buttonWrapper + ' mt-2'}>
          <SmallTextLink onClick={(e) => {
            e.preventDefault();
            setShowAll(!showAll);
          }}>{showAll ? 'See Less' : 'See More'}</SmallTextLink>
        </div>
      }
    </div>
    <div className='col-12'>
      <div className='card pt-4 pb-4 pe-4 ps-4' style={{
        border: 0,
        borderRadius: 0
      }}>
        <div className='flex'>
          {
            badges && badges.slice(0, showAll ? badges.length : 6).map((v, i) => {
              return <div className='col-4 col-lg-2' key={i}>
                <Link href='/Badges.aspx' className={s.badgeLink}>
                  <a>
                    <div className={s.imageWrapper}>
                      <span className={`icon-${v.name.toLowerCase().replace(/ /g, '-')}`} />
                    </div>
                    <p className={`${s.label} link2019 mb-0 `}>{v.name}</p>
                  </a>
                </Link>
              </div>
            })
          }
        </div>
      </div>
    </div>
  </div>
}

export default RobloxBadges;