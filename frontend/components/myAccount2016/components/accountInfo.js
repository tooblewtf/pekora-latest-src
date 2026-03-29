import React, { useRef } from "react";
import { createUseStyles } from "react-jss";
import getFlag from "../../../lib/getFlag";
import { setUserDescription } from "../../../services/accountInformation";
import {
    getAvPageStyle,
    getCatalogPageStyle,
	getFontStyle,
    getTheme,
    setAvPageStyle,
    setCatalogPageStyle,
	setFontStyle,
    setTheme
} from "../../../services/theme";
import AuthenticationStore from "../../../stores/authentication";
import useCardStyles from "../../userProfile/styles/card";
import MyAccountStore from "../stores/myAccountStore"
import useFormStyles from "../styles/forms";
import GenderSelection from "./genderSelection";
import Subtitle from "./subtitle";
import { setAvPageStyleReq } from "../../../services/accountSettings";

const useEditButtonStyles = createUseStyles({
    editButton: {
        float: 'right',
        color: '#666',
        cursor: 'pointer',
    },
})

const EditButton = (props) => {
    const s = useEditButtonStyles();
    return <span className={s.editButton} onClick={props.onClick}>Edit</span>
}

const AccountInfo = props => {
    const store = MyAccountStore.useContainer();
    const auth = AuthenticationStore.useContainer();
    const descRef = useRef(null);
    
    const cardStyles = useCardStyles();
    const s = useFormStyles();
    return <div className='row'>
        <div className='col-12 mt-2'>
            <Subtitle>Account Info</Subtitle>
            <div className={cardStyles.card + ' p-3'}>
                <p className={s.accountInfoLabel}>Username: <span className={s.accountInfoValue}>{auth.username}</span>
                    <EditButton onClick={() => {
                        store.setModal('CHANGE_USERNAME');
                    }}></EditButton></p>
                <p className={s.accountInfoLabel}>Password: <span className={s.accountInfoValue}>**********</span>
                    <EditButton onClick={() => {
                        store.setModal('CHANGE_PASSWORD');
                    }}></EditButton></p>
                <p className={s.accountInfoLabel}>Email Address: <span
                    className={s.accountInfoValue}>{store.email}</span> <EditButton onClick={() => {
                    store.setModal('CHANGE_EMAIL');
                }}></EditButton></p>
            </div>
        </div>
        <div className='col-12 mt-2'>
            <Subtitle>Personal</Subtitle>
            <div className={cardStyles.card + ' p-3'}>
                <textarea ref={descRef} className={s.descInput} rows={3} defaultValue={store.description}></textarea>
                <p className='mb-0 font-size-12'>Do not provide any details that can be used to identify you outside
                    Marine.</p>
                <div className='mt-1'>
                    <div className='row'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Birthday'
                                   readOnly={true} type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select}>
                                <option value='1'>January</option>
                                <option value='2'>February</option>
                                <option value='3'>March</option>
                                <option value='4'>April</option>
                                <option value='5'>May</option>
                                <option value='6'>June</option>
                                <option value='7'>July</option>
                                <option value='8'>August</option>
                                <option value='9'>September</option>
                                <option value='10'>October</option>
                                <option value='11'>November</option>
                                <option value='12'>December</option>
                            </select>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select}>
                                {[...new Array(31)].map((v, i) => {
                                    return <option value={i + 1} key={i}>{i + 1}</option>
                                })}
                            </select>
                        </div>
                        <div className='col ps-0'>
                            <select className={'form-control ' + s.select}>
                                {[...new Array(100)].map((v, i) => {
                                    return <option value={2016 - i} key={i}>{2016 - i}</option>
                                })}
                            </select>
                        </div>
                    </div>
                </div>
                <div className='mt-2'>
                    <div className='row'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Gender'
                                   readOnly={true} type='text'></input>
                        </div>
                        <GenderSelection id={2} displayName='Male'></GenderSelection>
                        <GenderSelection id={3} displayName='Female'></GenderSelection>
                    </div>
                </div>
                <div className='mt-1 mb-4'>
                    <div className={s.saveButtonWrapper}>
                        <button className={s.saveButton} onClick={() => {
                            // todo: gender, birthdate
                            setUserDescription({
                                newDescription: descRef.current.value,
                            });
                        }}>Save
                        </button>
                    </div>
                </div>
                <div className='mt-4 mb-4'>&emsp;</div>
            </div>
        </div>
        {getFlag('settingsPageThemeSelectorEnabled', false) &&
            <div className='col-12 mt-2'>
                <Subtitle>Extensions</Subtitle>
                <div className={cardStyles.card + ' p-3'}>
                    <div className='row mt-1'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Website Theme'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getTheme()} onChange={(ev) => {
                                setTheme(ev.currentTarget.value);
                                window.location.reload();
                            }}>
                                <option value='light'>Default</option>
                                <option value='obc2016'>OBC Theme</option>
                            </select>
                        </div>
                    </div>
                    <div className='row mt-2'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Avatar Page Style'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getAvPageStyle()} onChange={(ev) => {
                                setAvPageStyle(ev.currentTarget.value);
                                setAvPageStyleReq({ newAvatarPageStyle: ev.currentTarget.value }).then();
                                window.location.reload();
                            }}>
                                <option value="Modern">Modern (2017+)</option>
                                <option value="Legacy">Legacy (2013-2017)</option>
                            </select>
                        </div>
                    </div>
					<div className='row mt-2'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Website Font'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getFontStyle()}
                                    onChange={ev => {
                                        setFontStyle(ev.currentTarget.value);
                                        window.location.reload();
                                    }}>
                                <option value="Default">Default</option>
                                <option value="SourceSans">Source Sans</option>
								<option value="freaky">𝓯𝓻𝓮𝓪𝓴𝔂</option>
                            </select>
                        </div>
                    </div>
                    <div className='row mt-2'>
                        <div className='col pe-0'>
                            <input className={'form-control ' + s.select + ' ' + s.disabled} value='Catalog Page Style'
                                   readOnly={true}
                                   type='text'></input>
                        </div>
                        <div className='col ps-0 pe-0'>
                            <select className={'form-control ' + s.select} value={getCatalogPageStyle()}
                                    onChange={ev => {
                                        setCatalogPageStyle(ev.currentTarget.value);
                                        window.location.reload();
                                    }}>
                                <option value="Modern">Modern (2017+)</option>
                                <option value="Legacy">Legacy (2012-2017)</option>
                            </select>
                        </div>
                    </div>
                </div>
            </div>
        }
    </div>
}

export default AccountInfo;