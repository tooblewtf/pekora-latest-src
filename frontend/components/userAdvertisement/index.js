import { at } from "lodash";
import { useEffect, useRef, useState } from "react";
import { createUseStyles } from "react-jss";
import request, { getBaseUrl } from "../../lib/request";
import { adTypes } from "./constants";
import Link from "../link";

const useStyles = createUseStyles({
    adWrapper: {},
    adImage: {
        width: '100%',
        height: 'auto',
        margin: '0 auto',
        display: 'block',
        '@media(max-width: 800px)': {
            paddingTop: '10px',
            paddingBottom: '10px',
        },
    },
	adIdentification: {
        float: 'left',
        fontSize: '10px',
    },
	adIdentificationText: {
        color: '#191919',
        textTransform: 'uppercase',
    },
	BadAdButton: {
        color: '#757575',
        float: 'right',
		fontSize: '10px',
        '&:hover': {
          color: 'red',
        },
    },
})

/**
 * User advertisement iframe
 * @param {{type: number; wrapperClass?: string; backupWidth?: number;}} props
 */
const UserAdvertisement = props => {
    const info = adTypes[props.type];
    const [imageUrl, setImageUrl] = useState(null);
    const [link, setLink] = useState(null);
    const [title, setTitle] = useState(null);
    const [imageLoaded, setImageLoaded] = useState(false);
    const s = useStyles();
    
    // I HATE IFRAMES I HATE IFRAMES I HATE IFRAMES I HATE IFRAMES
    useEffect(() => {
        request('GET', `${getBaseUrl()}/user-sponsorship/${props.type}`).then(adData => {
            const doc = new DOMParser().parseFromString(adData.data, 'text/html');
            const imageElements = doc.getElementsByTagName('img');
            const aTags = doc.getElementsByTagName('a');
            if (!imageElements.length || !aTags.length) {
                console.error('[error] could not get an element from iframe:', imageElements, aTags);
                return;
            }
            const imageUrl = imageElements[0].getAttribute('src');
            const link = aTags[0].getAttribute('href');
            const adTitle = aTags[0].getAttribute('title');
            if (!imageUrl || !link || !adTitle) {
                console.error('[error] could not get an attribute from iframe: ', imageUrl, link, adTitle);
                return;
            }
            setImageUrl(imageUrl);
            setTitle(adTitle);
            setLink(link);
        }).catch(e => {
            console.error('[error] could not load user ad:', e);
        })
    }, []);
    
    // TODO: calculate correct height of ad when current screen width is smaller than ad width. The height is way too big on mobile.
    if (!info) throw new Error(`unexpected adType: ${props.type}`);
    if (!imageUrl) {
        return <div className={`${props?.wrapperClass || ''}`} style={{ width: props.backupWidth || '100%', height: info.height }}/>
    }
    return <div className={`${s.adWrapper} ${props?.wrapperClass || ''}`}
                style={imageLoaded ? undefined : { height: info.height, width: '100%' }}>
        <Link href={link || '#'}>
            <a title={title}>
                <img onLoad={() => {
                    setImageLoaded(true)
                }} src={imageUrl} className={s.adImage} style={{ maxWidth: info.width, maxHeight: info.height }}/>
            </a>
			</Link>
			<div>
			<span className={s.adIdentification}>
                <span className={s.adIdentificationText}>Advertisement</span>
            </span>
			<a className={s.BadAdButton} target='_top' href='https://www.silrev.biz/abusereport/Asset' title='click to report an offensive ad'>Report</a>
		</div>
    </div>
}

export default UserAdvertisement;